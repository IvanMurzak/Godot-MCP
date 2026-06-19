/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System;
using System.Diagnostics;
using com.IvanMurzak.Godot.MCP.Reflection;
using com.IvanMurzak.McpPlugin.Common.Utils;
using com.IvanMurzak.ReflectorNet;
using Godot;

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>
    /// A nested editor <see cref="Window"/> for testing object serialization through the MCP
    /// <see cref="Reflector"/> — the Godot analog of Unity-MCP's <c>SerializationCheckWindow</c>. The user
    /// picks a target (a scene <see cref="Node"/> via the current editor selection, or a
    /// <see cref="Resource"/> via the resource picker), toggles <c>Recursive</c>, presses <b>Serialize</b>,
    /// and inspects the pretty-printed JSON the reflector produced. Layout, top → bottom: an "Information"
    /// foldout (collapsed), a divider, the input row (target picker + "Use Editor Selection" + Recursive
    /// toggle + Serialize), a divider, an "Output (NN ms)" header, the scrollable monospace JSON output, and
    /// a floating "Copy" button (bottom-right) that flips to "Copied!" for ~1.5s.
    ///
    /// <para>
    /// The reflector is read from the ambient <see cref="GodotMcpReflector.GetOrCreate"/> (the same converter
    /// set the connection registered, with a default-built fallback when no connection is live), mirroring
    /// Unity's <c>UnityMcpPluginEditor.Instance.Reflector</c>. The <see cref="Reflector.Serialize"/> call
    /// shape (<c>obj</c>, <c>name</c>, <c>recursive</c>) matches the existing Godot tool handlers
    /// (e.g. <c>resource-get-data</c>).
    /// </para>
    ///
    /// <para>
    /// Editor-only (<c>#if TOOLS</c>): it builds live Godot UI <see cref="Node"/>s and reads the editor
    /// selection, so it is verified via the headless Godot smoke (<c>test.md</c> Suite 3), not the plain-xUnit
    /// host. Every Godot-signal connection is an OBJECT+METHOD <see cref="Callable"/> (<c>new Callable(this,
    /// MethodName.…)</c>), never a C# delegate — so it is not a ManagedCallable and cannot trigger the
    /// <c>delegate_handle.value == nullptr</c> Build-Project hot-reload flood (mirrors the dock-wide discipline).
    /// </para>
    /// </summary>
    [Tool]
    public partial class SerializationCheckWindow : Window
    {
        // The information block shown in the collapsed foldout — what serialization is, how to use the window.
        const string InformationText =
            "This window serializes a Godot object through the MCP reflector (ReflectorNet) — the exact same " +
            "serializer the MCP tools use — and shows the resulting JSON.\n\n" +
            "• Target: pick a Resource with the picker, or select a Node in the scene tree and press " +
            "\"Use Editor Selection\".\n" +
            "• Recursive: ON serializes nested / child references; OFF stays shallow.\n" +
            "• Set the dock's Log Level to Debug for the reflector's detailed trace in the Output panel.";

        EditorResourcePicker _resourcePicker = null!;
        Label _selectionLabel = null!;
        CheckButton _recursiveToggle = null!;
        Label _outputHeader = null!;
        TextEdit _outputText = null!;
        Button _copyButton = null!;

        // The current target. A Node chosen from the editor selection takes precedence over the resource
        // picker's value; cleared back to the picker's Resource when the picker changes.
        GodotObject? _target;
        string _fullOutputText = string.Empty;

        // The Copy button's label before the transient "Copied!" flash, restored by the timer's object+method
        // handler (OnCopyFlashTimeout) ~1.5s later. Stored on the instance so the handler needs no captured state.
        string _copyButtonOriginalText = "Copy";

        public SerializationCheckWindow()
        {
            Name = "SerializationCheckWindow";
            Title = "Serialization Check";
            Size = new Vector2I(460, 560);
            MinSize = new Vector2I(400, 500);
            Unresizable = false;

            // Free the window when the OS/editor close button (the X) is pressed — no leaks. Object+method
            // Callable (not a delegate +=) so it never enters the ManagedCallable hot-reload registry.
            Connect(Window.SignalName.CloseRequested, new Callable(this, MethodName.OnClosePressed));

            BuildUi();
        }

        void BuildUi()
        {
            var margin = new MarginContainer { Name = "Margin" };
            margin.AddThemeConstantOverride("margin_left", 8);
            margin.AddThemeConstantOverride("margin_right", 8);
            margin.AddThemeConstantOverride("margin_top", 8);
            margin.AddThemeConstantOverride("margin_bottom", 8);
            margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(margin);

            var root = new VBoxContainer { Name = "Root" };
            root.AddThemeConstantOverride("separation", 6);
            margin.AddChild(root);

            // --- Information foldout (collapsed) --------------------------------------------------------------
            var (infoFoldout, infoBody) = DockStyle.Foldout("Information");
            var infoLabel = new Label
            {
                Name = "InformationText",
                Text = InformationText,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            DockStyle.ApplyDescription(infoLabel);
            infoBody.AddChild(infoLabel);
            root.AddChild(infoFoldout);

            root.AddChild(DockStyle.Divider("InfoDivider"));

            // --- Input row: target picker + "Use Editor Selection" + Recursive + Serialize -------------------
            var pickerLabel = new Label { Name = "TargetLabel", Text = "Target" };
            DockStyle.ApplyDescription(pickerLabel);
            root.AddChild(pickerLabel);

            _resourcePicker = new EditorResourcePicker
            {
                Name = "TargetPicker",
                BaseType = nameof(Resource),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            // Object+method Callable (not a delegate +=) so it never enters the ManagedCallable hot-reload registry.
            _resourcePicker.Connect(EditorResourcePicker.SignalName.ResourceChanged, new Callable(this, MethodName.OnResourcePicked));
            root.AddChild(_resourcePicker);

            var selectionRow = new HBoxContainer { Name = "SelectionRow", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            selectionRow.AddThemeConstantOverride("separation", 6);

            var useSelectionButton = new Button
            {
                Name = "UseSelection",
                Text = "Use Editor Selection",
                MouseDefaultCursorShape = Control.CursorShape.PointingHand
            };
            DockStyle.ApplySecondaryButton(useSelectionButton);
            DockStyle.ConnectPressed(useSelectionButton, this, MethodName.OnUseSelectionPressed);
            selectionRow.AddChild(useSelectionButton);

            _selectionLabel = new Label
            {
                Name = "SelectionLabel",
                Text = string.Empty,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            DockStyle.ApplyDescription(_selectionLabel);
            selectionRow.AddChild(_selectionLabel);
            root.AddChild(selectionRow);

            var actionRow = new HBoxContainer { Name = "ActionRow", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            actionRow.AddThemeConstantOverride("separation", 8);

            _recursiveToggle = new CheckButton { Name = "RecursiveToggle", Text = "Recursive", ButtonPressed = true };
            actionRow.AddChild(_recursiveToggle);

            actionRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

            var serializeButton = new Button
            {
                Name = "Serialize",
                Text = "Serialize",
                MouseDefaultCursorShape = Control.CursorShape.PointingHand
            };
            DockStyle.ApplyCompactPrimaryButton(serializeButton);
            DockStyle.ConnectPressed(serializeButton, this, MethodName.OnSerializePressed);
            actionRow.AddChild(serializeButton);
            root.AddChild(actionRow);

            root.AddChild(DockStyle.Divider("OutputDivider"));

            // --- Output header + scrollable monospace JSON output (with the floating Copy button) ------------
            _outputHeader = new Label { Name = "OutputHeader", Text = "Output" };
            DockStyle.ApplySectionTitle(_outputHeader);
            root.AddChild(_outputHeader);

            // The output area + the floating Copy button share one cell so Copy floats over the JSON's
            // bottom-right corner (Unity anchors it bottom-right of the output container).
            var outputCell = new MarginContainer
            {
                Name = "OutputCell",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            root.AddChild(outputCell);

            _outputText = new TextEdit
            {
                Name = "OutputText",
                Editable = false,
                ScrollFitContentHeight = false,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                PlaceholderText = "Serialize a target to see its ReflectorNet JSON here."
            };
            ApplyMonospaceOutputStyle(_outputText);
            outputCell.AddChild(_outputText);

            // Floating Copy button, bottom-right of the output cell.
            var copyAnchor = new Control
            {
                Name = "CopyAnchor",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            outputCell.AddChild(copyAnchor);

            _copyButton = new Button
            {
                Name = "Copy",
                Text = "Copy",
                MouseDefaultCursorShape = Control.CursorShape.PointingHand
            };
            DockStyle.ApplySecondaryButton(_copyButton);
            _copyButton.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
            _copyButton.OffsetLeft = -88;
            _copyButton.OffsetTop = -34;
            _copyButton.OffsetRight = -8;
            _copyButton.OffsetBottom = -8;
            DockStyle.ConnectPressed(_copyButton, this, MethodName.OnCopyPressed);
            copyAnchor.AddChild(_copyButton);
        }

        /// <summary>Skin the output <see cref="TextEdit"/> as a subtle dark, rounded, monospace JSON panel.</summary>
        static void ApplyMonospaceOutputStyle(TextEdit textEdit)
        {
            var box = new StyleBoxFlat { BgColor = DockStyle.Rgb(DockTheme.WindowBackground).Darkened(0.25f) };
            box.SetCornerRadiusAll(8);
            box.ContentMarginLeft = 8;
            box.ContentMarginRight = 8;
            box.ContentMarginTop = 8;
            box.ContentMarginBottom = 8;
            textEdit.AddThemeStyleboxOverride("normal", box);
            textEdit.AddThemeStyleboxOverride("read_only", box);

            // Monospace face from the editor theme when available (degrades to the default font headless).
            try
            {
                var theme = EditorInterface.Singleton?.GetEditorTheme();
                if (theme != null && theme.HasFont("source", "EditorFonts"))
                    textEdit.AddThemeFontOverride("font", theme.GetFont("source", "EditorFonts"));
            }
            catch { /* no editor theme (headless/test) — stay default font */ }
        }

        /// <summary>The resource picker changed → make that Resource the target and clear any node selection.</summary>
        public void OnResourcePicked(Resource? resource)
        {
            _target = resource;
            if (resource != null)
                _selectionLabel.Text = string.Empty;
        }

        /// <summary>
        /// Adopt the first node of the current editor selection as the target. Clears the resource picker so
        /// the two pickers do not silently disagree about which object is "the target".
        /// </summary>
        public void OnUseSelectionPressed()
        {
            Node? node = null;
            try
            {
                var nodes = EditorInterface.Singleton?.GetSelection()?.GetSelectedNodes();
                if (nodes != null && nodes.Count > 0)
                    node = nodes[0];
            }
            catch (Exception ex)
            {
                _selectionLabel.Text = $"Selection unavailable: {ex.Message}";
                return;
            }

            if (node == null)
            {
                _selectionLabel.Text = "No node selected in the scene tree.";
                return;
            }

            _target = node;
            _resourcePicker.EditedResource = null; // node target wins; keep the pickers consistent.
            _selectionLabel.Text = $"Node: {node.Name} ({node.GetClass()})";
        }

        public void OnSerializePressed()
        {
            // The resource picker is the target when no node was explicitly adopted via "Use Editor Selection".
            var target = _target ?? _resourcePicker.EditedResource;
            var recursive = _recursiveToggle.ButtonPressed;

            // A null target is fine (TargetName returns null and the reflector serializes it), but a
            // non-null target the user deleted after adopting it is a freed GodotObject — marshalling that
            // into Serialize is a native access to freed memory that is not guaranteed to surface as a
            // catchable managed exception. Reject it cleanly before reaching the reflector.
            if (target != null && !GodotObject.IsInstanceValid(target))
            {
                _outputHeader.Text = "Output";
                SetOutput("Target is no longer valid (was it deleted?)");
                return;
            }

            try
            {
                var reflector = GodotMcpReflector.GetOrCreate();
                var name = TargetName(target);

                var stopwatch = Stopwatch.StartNew();
                var serialized = reflector.Serialize(
                    obj: target,
                    name: name,
                    recursive: recursive);
                var json = serialized.ToPrettyJson();
                stopwatch.Stop();

                _outputHeader.Text = $"Output ({stopwatch.ElapsedMilliseconds} ms)";
                SetOutput(json);
            }
            catch (Exception ex)
            {
                _outputHeader.Text = "Output";
                SetOutput($"Error: {ex.Message}\n\n{ex.StackTrace}");
                GD.PrintErr($"[Godot-MCP] Serialization Check failed: {ex}");
            }
        }

        /// <summary>A human-readable name for the serialized member, mirroring the tool handlers' naming.</summary>
        static string? TargetName(GodotObject? target)
        {
            switch (target)
            {
                case null:
                    return null;
                case Node node:
                    return node.Name;
                case Resource resource:
                    return string.IsNullOrEmpty(resource.ResourceName)
                        ? (string.IsNullOrEmpty(resource.ResourcePath) ? resource.GetClass() : resource.ResourcePath)
                        : resource.ResourceName;
                default:
                    return target.GetClass();
            }
        }

        void SetOutput(string text)
        {
            _fullOutputText = text ?? string.Empty;
            _outputText.Text = _fullOutputText;
        }

        public void OnCopyPressed()
        {
            DisplayServer.ClipboardSet(_fullOutputText);

            _copyButtonOriginalText = _copyButton.Text;
            _copyButton.Text = "Copied!";

            // Flip the label back after ~1.5s via an OBJECT+METHOD Callable to this window's instance handler (no
            // delegate Callable). The handler re-checks validity so a window closed mid-flash never touches a
            // freed control. The timer fires once, so a single connection is fine.
            var timer = GetTree().CreateTimer(1.5);
            timer.Connect(SceneTreeTimer.SignalName.Timeout, new Callable(this, MethodName.OnCopyFlashTimeout));
        }

        /// <summary>
        /// Restore the Copy button's label after the "Copied!" flash. Connected to the one-shot timer's
        /// <c>timeout</c> via an object+method <see cref="Callable"/>. Re-checks the button's validity so a window
        /// closed mid-flash never touches a freed control.
        /// </summary>
        public void OnCopyFlashTimeout()
        {
            if (IsInstanceValid(_copyButton))
                _copyButton.Text = _copyButtonOriginalText;
        }

        /// <summary>Pop the window up centred and visible. Called by the footer after parenting it into the tree.</summary>
        public void PopupCenteredAndShow()
        {
            PopupCentered(Size);
        }

        public void OnClosePressed()
        {
            // QueueFree frees the window and all children next idle frame — no leaks.
            QueueFree();
        }
    }
}
#endif
