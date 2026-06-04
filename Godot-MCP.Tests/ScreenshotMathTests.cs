/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System;
using com.IvanMurzak.Godot.MCP.Tools;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pure-managed coverage for the screenshot tool family's math/validation helper
    /// (<see cref="ScreenshotMath"/>): dimension validation, transport-cap clamping (aspect preserved),
    /// framing validation, the camera-distance trig, the view-direction table, and hex-color parsing. None
    /// of this touches a live Godot rendering device, so it runs in the plain xUnit host with no Godot
    /// binary. The editor-driving handlers themselves (<c>Tool_Screenshot.*.cs</c>, behind <c>#if TOOLS</c>)
    /// — which read back and PNG-encode a real Godot <c>Image</c> — are verified by the headless/windowed
    /// Godot smoke (test.md Suite 3), since image encoding needs the Godot runtime.
    /// </summary>
    public class ScreenshotMathTests
    {
        // ---- ValidateDimensions ------------------------------------------------------------------

        [Theory]
        [InlineData(1, 1, true)]
        [InlineData(1920, 1080, true)]
        [InlineData(ScreenshotMath.MaxDimension, ScreenshotMath.MaxDimension, true)]
        [InlineData(0, 100, false)]
        [InlineData(100, 0, false)]
        [InlineData(-1, 100, false)]
        [InlineData(ScreenshotMath.MaxDimension + 1, 100, false)]
        [InlineData(100, ScreenshotMath.MaxDimension + 1, false)]
        public void ValidateDimensions_EnforcesBounds(int width, int height, bool expectedValid)
        {
            var valid = ScreenshotMath.ValidateDimensions(width, height, out var error);
            Assert.Equal(expectedValid, valid);
            if (expectedValid)
                Assert.Null(error);
            else
                Assert.False(string.IsNullOrEmpty(error));
        }

        // ---- ClampToTransportLimit ---------------------------------------------------------------

        [Fact]
        public void ClampToTransportLimit_WithinCap_ReturnsUnchanged()
        {
            var (w, h) = ScreenshotMath.ClampToTransportLimit(1920, 1080);
            Assert.Equal(1920, w);
            Assert.Equal(1080, h);
        }

        [Fact]
        public void ClampToTransportLimit_AtCap_ReturnsUnchanged()
        {
            var (w, h) = ScreenshotMath.ClampToTransportLimit(
                ScreenshotMath.MaxScreenshotDimension, ScreenshotMath.MaxScreenshotDimension);
            Assert.Equal(ScreenshotMath.MaxScreenshotDimension, w);
            Assert.Equal(ScreenshotMath.MaxScreenshotDimension, h);
        }

        [Fact]
        public void ClampToTransportLimit_OversizedLandscape_ScalesLongestEdgeToCap_PreservesAspect()
        {
            // 8000x4000 (2:1) -> longest edge clamped to the cap, aspect preserved.
            var (w, h) = ScreenshotMath.ClampToTransportLimit(8000, 4000);
            Assert.Equal(ScreenshotMath.MaxScreenshotDimension, w);
            Assert.Equal(ScreenshotMath.MaxScreenshotDimension / 2, h);
        }

        [Fact]
        public void ClampToTransportLimit_OversizedPortrait_ScalesLongestEdgeToCap_PreservesAspect()
        {
            var (w, h) = ScreenshotMath.ClampToTransportLimit(4000, 8000);
            Assert.Equal(ScreenshotMath.MaxScreenshotDimension / 2, w);
            Assert.Equal(ScreenshotMath.MaxScreenshotDimension, h);
        }

        [Fact]
        public void ClampToTransportLimit_ExtremeAspect_FloorsShortEdgeAtOne()
        {
            // A 1-px-tall band scaled down must never collapse the short edge to 0.
            var (w, h) = ScreenshotMath.ClampToTransportLimit(16000, 1);
            Assert.Equal(ScreenshotMath.MaxScreenshotDimension, w);
            Assert.True(h >= 1);
        }

        // ---- ValidateFraming ---------------------------------------------------------------------

        [Theory]
        [InlineData(60f, 0.05f, 1000f, 1.2f, true)]
        [InlineData(ScreenshotMath.MinFieldOfView, 0.05f, 1000f, 1.2f, true)]
        [InlineData(ScreenshotMath.MaxFieldOfView, 0.05f, 1000f, 1.2f, true)]
        // fov out of range
        [InlineData(0f, 0.05f, 1000f, 1.2f, false)]
        [InlineData(180f, 0.05f, 1000f, 1.2f, false)]
        // near must be > 0
        [InlineData(60f, 0f, 1000f, 1.2f, false)]
        [InlineData(60f, -1f, 1000f, 1.2f, false)]
        // far must be > near
        [InlineData(60f, 10f, 10f, 1.2f, false)]
        [InlineData(60f, 10f, 5f, 1.2f, false)]
        // padding out of range
        [InlineData(60f, 0.05f, 1000f, 0f, false)]
        [InlineData(60f, 0.05f, 1000f, 1000f, false)]
        public void ValidateFraming_EnforcesRanges(float fov, float near, float far, float padding, bool expectedValid)
        {
            var valid = ScreenshotMath.ValidateFraming(fov, near, far, padding, out var error);
            Assert.Equal(expectedValid, valid);
            if (!expectedValid)
                Assert.False(string.IsNullOrEmpty(error));
        }

        [Fact]
        public void ValidateFraming_RejectsNonFinite()
        {
            Assert.False(ScreenshotMath.ValidateFraming(float.NaN, 0.05f, 1000f, 1.2f, out _));
            Assert.False(ScreenshotMath.ValidateFraming(60f, float.PositiveInfinity, 1000f, 1.2f, out _));
            Assert.False(ScreenshotMath.ValidateFraming(60f, 0.05f, float.NaN, 1.2f, out _));
            Assert.False(ScreenshotMath.ValidateFraming(60f, 0.05f, 1000f, float.PositiveInfinity, out _));
        }

        // ---- ComputeCameraDistance ---------------------------------------------------------------

        [Fact]
        public void ComputeCameraDistance_LargerRadius_YieldsLargerDistance()
        {
            var near = ScreenshotMath.ComputeCameraDistance(1f, 60f, 1.0f);
            var far = ScreenshotMath.ComputeCameraDistance(2f, 60f, 1.0f);
            Assert.True(far > near);
            // Distance is linear in radius for a fixed fov/padding.
            Assert.Equal(near * 2f, far, 3);
        }

        [Fact]
        public void ComputeCameraDistance_MorePadding_YieldsLargerDistance()
        {
            var tight = ScreenshotMath.ComputeCameraDistance(1f, 60f, 1.0f);
            var loose = ScreenshotMath.ComputeCameraDistance(1f, 60f, 1.5f);
            Assert.True(loose > tight);
            Assert.Equal(tight * 1.5f, loose, 3);
        }

        [Fact]
        public void ComputeCameraDistance_ZeroRadius_StillFinitePositive()
        {
            var d = ScreenshotMath.ComputeCameraDistance(0f, 60f, 1.2f);
            Assert.True(d > 0f);
            Assert.True(float.IsFinite(d));
        }

        [Fact]
        public void ComputeCameraDistance_KnownFov_MatchesTrig()
        {
            // For fov=60deg, half-angle=30deg, sin(30)=0.5 => distance = radius*padding / 0.5 = 2*radius*padding.
            var d = ScreenshotMath.ComputeCameraDistance(3f, 60f, 1.0f);
            Assert.Equal(6f, d, 3);
        }

        // ---- BracketClipPlanes -------------------------------------------------------------------

        [Fact]
        public void BracketClipPlanes_ObjectAlwaysWithinRange()
        {
            // Camera 100 units out from a radius-10 object: the object spans depths [90, 110]. The returned
            // planes must bracket that whole span.
            var (near, far) = ScreenshotMath.BracketClipPlanes(100f, 10f, userNear: 0.05f, userFar: 4000f);
            Assert.True(near <= 90f, $"near {near} must be <= object front face 90");
            Assert.True(far >= 110f, $"far {far} must be >= object back face 110");
            Assert.True(near > 0f);
            Assert.True(far > near);
        }

        [Fact]
        public void BracketClipPlanes_OnlyLoosensCallerPlanes()
        {
            // A wide user range already brackets the object → it must be honored unchanged (only loosened).
            var (near, far) = ScreenshotMath.BracketClipPlanes(50f, 5f, userNear: 0.01f, userFar: 10000f);
            Assert.Equal(0.01f, near, 5);
            Assert.Equal(10000f, far, 1);
        }

        [Fact]
        public void BracketClipPlanes_LargeObjectSmallFar_GrowsFar()
        {
            // A big object with a too-small user far: far must grow to clear the back face (~210), not stay 100.
            var (near, far) = ScreenshotMath.BracketClipPlanes(200f, 100f, userNear: 0.05f, userFar: 100f);
            Assert.True(far >= 300f, $"far {far} must clear the back face ~300");
            Assert.True(near > 0f);
        }

        [Fact]
        public void BracketClipPlanes_NearStaysPositive_ForCloseLargeObject()
        {
            // Object front face would be negative (distance < radius): near must floor at a small positive.
            var (near, far) = ScreenshotMath.BracketClipPlanes(5f, 20f, userNear: 0.05f, userFar: 10f);
            Assert.True(near > 0f, $"near {near} must stay positive");
            Assert.True(far > near, $"far {far} must exceed near {near}");
            Assert.True(float.IsFinite(near) && float.IsFinite(far));
        }

        // ---- GetViewDirectionAndUp ---------------------------------------------------------------

        [Theory]
        [InlineData(ScreenshotView.Front, 0f, 0f, -1f)]
        [InlineData(ScreenshotView.Back, 0f, 0f, 1f)]
        [InlineData(ScreenshotView.Left, -1f, 0f, 0f)]
        [InlineData(ScreenshotView.Right, 1f, 0f, 0f)]
        [InlineData(ScreenshotView.Top, 0f, 1f, 0f)]
        [InlineData(ScreenshotView.Bottom, 0f, -1f, 0f)]
        public void GetViewDirectionAndUp_ReturnsExpectedDirection(ScreenshotView view, float dx, float dy, float dz)
        {
            var (dir, up) = ScreenshotMath.GetViewDirectionAndUp(view);
            Assert.Equal(dx, dir.x, 5);
            Assert.Equal(dy, dir.y, 5);
            Assert.Equal(dz, dir.z, 5);

            // The up vector must not be parallel to the view direction (else LookAt is degenerate). For
            // top/bottom the up is forward (Z); for the lateral views it is world up (Y).
            var dot = dir.x * up.x + dir.y * up.y + dir.z * up.z;
            Assert.Equal(0f, dot, 5);
        }

        // ---- TryParseHtmlColor -------------------------------------------------------------------

        [Fact]
        public void TryParseHtmlColor_SixDigit_Parses()
        {
            Assert.True(ScreenshotMath.TryParseHtmlColor("#404040", out var c));
            Assert.Equal(0x40 / 255f, c.r, 5);
            Assert.Equal(0x40 / 255f, c.g, 5);
            Assert.Equal(0x40 / 255f, c.b, 5);
            Assert.Equal(1f, c.a, 5);
        }

        [Fact]
        public void TryParseHtmlColor_NoHash_Parses()
        {
            Assert.True(ScreenshotMath.TryParseHtmlColor("FF0000", out var c));
            Assert.Equal(1f, c.r, 5);
            Assert.Equal(0f, c.g, 5);
            Assert.Equal(0f, c.b, 5);
        }

        [Fact]
        public void TryParseHtmlColor_EightDigit_ParsesAlpha()
        {
            Assert.True(ScreenshotMath.TryParseHtmlColor("#00FF0080", out var c));
            Assert.Equal(0f, c.r, 5);
            Assert.Equal(1f, c.g, 5);
            Assert.Equal(0f, c.b, 5);
            Assert.Equal(0x80 / 255f, c.a, 5);
        }

        [Fact]
        public void TryParseHtmlColor_ThreeDigitShorthand_Parses()
        {
            // '#FFF' -> white (each nibble doubled).
            Assert.True(ScreenshotMath.TryParseHtmlColor("#FFF", out var c));
            Assert.Equal(1f, c.r, 5);
            Assert.Equal(1f, c.g, 5);
            Assert.Equal(1f, c.b, 5);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("#GGGGGG")]   // non-hex digits
        [InlineData("#12345")]    // wrong length (5)
        [InlineData("#1234567")]  // wrong length (7)
        [InlineData("notacolor")]
        public void TryParseHtmlColor_Invalid_ReturnsFalse(string? hex)
        {
            Assert.False(ScreenshotMath.TryParseHtmlColor(hex, out _));
        }
    }
}
