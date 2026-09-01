using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface;
using TerraFX.Interop.Windows;

namespace BossMod;

// note on coordinate systems:
// - world coordinates - X points West to East, Z points North to South - so SE is corner with both maximal coords, NW is corner with both minimal coords
//                       rotation 0 corresponds to South, and increases counterclockwise (so East is +pi/2, North is pi, West is -pi/2)
// - camera azimuth 0 correpsonds to camera looking North and increases counterclockwise
// - screen coordinates - X points left to right, Y points top to bottom
[SkipLocalsInit]
public sealed class MiniArena(WPos center, ArenaBounds bounds)
{
    public static readonly BossModuleConfig Config = Service.Config.Get<BossModuleConfig>();
    private WPos _center = center;
    private Vector3 _cameraCenter = Vector3.Zero; // Gets the values assigned later using arena centerXZ and player y. Makes projected radar hop when player hops
    public const float MaxApproxError = CurveApprox.ScreenError;
    public int _projectedThickness = 10; // Used for thickness of projected lines so it is changed in one place. Should have ui button if this goes anywhere.


    public WPos Center
    {
        get => _center;
        set
        {
            if (_center != value)
            {
                _center = value;
            }
        }
    }

    private ArenaBounds _bounds = bounds;
    public ArenaBounds Bounds
    {
        get => _bounds;
        set
        {
            if (!ReferenceEquals(_bounds, value))
            {
                _bounds = value;
                _bounds.ScreenHalfSize = ScreenHalfSize; // ensure arena bounds are fully initialized before doing anything else
            }
        }
    }

    public float ScreenHalfSize => 150f * Config.ArenaScale;
    //public float DisplayScreenHalfSize => 1920;

    public float ScreenMarginSize => 20f * Config.ArenaScale;

    // these are set at the beginning of each draw
    public Vector2 ScreenCenter;// = new Vector2(0,0);
    private Angle _cameraAzimuth;
    private float _cameraSinAzimuth;
    private float _cameraCosAzimuth = 1f;
    private float _overlayCenterX;
    private float _overlayCenterY;
    private float _overlayCenterZ;


    // Frame-constant rendering state, populated once by Begin().
    private float _scaledCos;
    private float _scaledSin;
    private float _frameArenaScale = 1f;
    private float _frameThicknessScale = 1f;
    private float _frameActorScale = 1f;
    private float _frameScreenHalfSize;
    private float _frameScreenMarginSize;
    private float _frameCardinalsFontSize = 17f;
    private bool _frameShowOutlinesAndShadows;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool InBounds(WPos position) => _bounds.Contains(position - _center);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WPos ClampToBounds(WPos position) => _center + _bounds.ClampToBounds(position - _center);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float IntersectRayBounds(WPos rayOrigin, in WDir rayDir) => _bounds.IntersectRay(rayOrigin - _center, rayDir);

    // prepare for drawing - set up internal state, clip rect etc.
    public void Begin(Angle cameraAzimuth)
    {
        // Snapshot renderer-facing configuration once per arena frame. Most primitive methods are hot
        // and do not need to re-read the config object for values that cannot meaningfully change
        // halfway through one Begin/End pair.
        var arenaScale = Config.ArenaScale;
        _frameArenaScale = arenaScale;
        _frameThicknessScale = Config.ThicknessScale;
        _frameActorScale = Config.ActorScale;
        _frameShowOutlinesAndShadows = Config.ShowOutlinesAndShadows;
        _frameCardinalsFontSize = Config.CardinalsFontSize;
        var screenHalfSize = _frameScreenHalfSize = 150f * arenaScale;
        var screenMarginSize = _frameScreenMarginSize = 20f * arenaScale;

        // This is the offset if using the radar window
        var centerOffset = new Vector2(screenMarginSize + Config.SlackForRotations * screenHalfSize);
        var fullSize = 2f * centerOffset;
        var currentWindowSize = ImGui.GetWindowSize();
        // Grab the display size for when I figure out how to put the drawings in world.
        var currentWindowScreenSize = ImGui.GetIO().DisplaySize;;
        var requiredWindowSize = Vector2.Max(fullSize, currentWindowSize);
        // Trying to adjust for using display size
        var displayOffsetX = currentWindowScreenSize.X / 2;
        var displayOffsetY = currentWindowScreenSize.Y / 2;
        var displayCenterOffset = new Vector2(displayOffsetX, displayOffsetY);
        // requiredWindowScreenSize is probably irrelevant because we want to use the whole screen size of the game window.
        var requiredWindowScreenSize = Vector2.Max(currentWindowScreenSize, currentWindowScreenSize);
        ImGui.SetWindowSize(requiredWindowSize);
        // Doing it this way just makes the radar window the size of the display rather than treating the display as a window.
        //ImGui.SetWindowSize(requiredWindowScreenSize);
        var cursor = ImGui.GetCursorScreenPos();
        //var cursor = new Vector2(_center.X, _center.Z);

        ImGui.Dummy(fullSize);

        if (_bounds.ScreenHalfSize != screenHalfSize)
        {
            _bounds.ScreenHalfSize = screenHalfSize;
        }

        var screenCenter = cursor + centerOffset;
        //var screenCenter = cursor;

        ScreenCenter = screenCenter;

        _cameraAzimuth = cameraAzimuth;
        (_cameraSinAzimuth, _cameraCosAzimuth) = MathF.SinCos(_cameraAzimuth.Rad);
        //_cameraAzimuth = cameraAzimuth;
        /*if (Camera.Instance != null)
        {
            _cameraAzimuth = Camera.Instance!.CameraAzimuth.Radians();
            (_cameraSinAzimuth, _cameraCosAzimuth) = MathF.SinCos(_cameraAzimuth.Rad);
        }
        else
        {
            ImGui.Text("No camera instance to grab azimuth from");
        }*/

        var screenScale = screenHalfSize * _bounds.InvRadius;
        var displayScreenScale = screenHalfSize * _bounds.InvRadius;
        var scaledCos = _cameraCosAzimuth * screenScale;
        var scaledSin = _cameraSinAzimuth * screenScale;
        //var screenScale = _bounds.InvRadius;
        //var scaledCos = _cameraCosAzimuth;
        //var scaledSin = _cameraSinAzimuth ;
        var centerX = screenCenter.X;
        //var centerX = 1;
        var centerY = screenCenter.Y;
        //var centerY = 1;

        _scaledCos = scaledCos;
        _scaledSin = scaledSin;

        var drawList = ImGui.GetWindowDrawList();
        var projectedDrawList = ImGui.GetWindowDrawList();

        var wmin = ImGui.GetWindowPos();
        //var wmin = Camera.Instance!.Origin.XZ();
        var wmax = wmin + ImGui.GetWindowSize();
        //var wmax = wmin + Camera.Instance!.ViewportSize;

        //TODO this drawlist is generated with the gui in mind. Change it to have the option to just not care about gui window for radar.
        // the drawlist gets pushed to Dx11ArenaRenderer.BeginArena(drawList, ...)
        drawList.PushClipRect(Vector2.Max(cursor, wmin), Vector2.Min(cursor + fullSize, wmax));

        // try pulling the screen size for use here

        //float width = screenSize.X;
        //float height = screenSize.Y;
        // successful but still mini. showed the character arrows and radar elements on screen without the ui window above it. and not in world space
        // just where ui would have been. So it was calculating for the window still
        //drawList.PushClipRect(new Vector2(_center.X, _center.Z), new Vector2(ImGui.GetIO().DisplaySize.X, ImGui.GetIO().DisplaySize.Y));
        //drawList.PushClipRect(wmin, wmax);
        //projectedDrawList.PushClipRectFullScreen(); // ends up in the corner still. Seems like the coordinates in BeginArena must be doing it?
        // original drawList code
        //drawList.PushClipRect(Vector2.Max(cursor, wmin), Vector2.Min(cursor + fullSize, wmax));



        // Start our custom DX11 arena renderer. Arena background, border and stencil clipping all
        // share the cached arena SDF. Arena shapes are considered immutable, so object identity is the cache key.
        //var worldPos = WorldPositionToScreenPosition(center);

        //Dx11ArenaRenderer.BeginArena(drawList, _bounds.Shape, _center.X, center.Z, _scaledCos, _scaledSin, screenScale);
        // Screen Scale determines the size of the arena drawing. Maybe 100 is closer to zero.
        // center.X and center.Y keep pegging to the upper left corder of the display window which is probably? (0,0) in this case.
        // need to figure out how to peg them to the arena center from the loaded module.
        //Dx11ArenaRenderer.BeginArena(drawList, _bounds.Shape, Camera.Instance.Origin.X, Camera.Instance.Origin.Z, 10, 10, 10);
        // Try hardcoded 1920, 1080 for the 4k display center coordinates -> this works!
        // Next try screen scale numbers. You can move it up to 20, but then it exceeds the invisible window size and you only see what isn't cut off.
        // The scaling numbers should move together. if you want to see the whole thing then you need to scaleCos,scaleSin, screenScale similarly so they are all visible.
        // For overlay it will need to match whatever the camera viewport information is instead of an invisible imgui window.
        // It isn't pegged to the actual spots on the ground either.
        //Dx11ArenaRenderer.BeginArena(drawList, _bounds.Shape, 1920, 1080, 10, 10, 10);
        // BeginArena takes the centerX and centerY as explicit display coordinates instead of in game view coordinates. So 0,0 is the top left corner like display convention.
        //Dx11ArenaRenderer.BeginArena(drawList, _bounds.Shape, 1920, 1080, scaledCos, scaledSin, screenScale);
        //Dx11ArenaRenderer.BeginArena(drawList, _bounds.Shape, centerX, centerY, _scaledCos, _scaledSin, screenScale);

        if (Service.ObjectTable.LocalPlayer != null)
        {
            var newX = Service.ObjectTable.LocalPlayer.Position.X;
            var newY = Service.ObjectTable.LocalPlayer.Position.Y;
            var newZ = Service.ObjectTable.LocalPlayer.Position.Z;

            _overlayCenterX = newX;
            _overlayCenterY = newY;
            _overlayCenterZ = newZ;
        }

        _cameraCenter = new (_center.X, _overlayCenterY, _center.Z);

        //var dl = ImGui.GetWindowDrawList();
        // DrawWorldPrimitives(cameraCenter, drawList) is not compatible with the overlay. Neither draw if you use it. If using BeginScreenBatch
        // If drawWorldPrimitives is modified to use BeginWorldBatch then overlay will only show behind radar window as a viewport
        // Could potentially double everything for drawing in Camera draw if you really wanted, but iono, maybe that is silly. Doesn't really seem like another way to hook it all though.
        // fixed drawWorldPrimitives so that it will draw with overlay on also. Still does not draw the aoe shapes at all.

        // Project the arena bounds onto the arena
        if (Config.ShowWorldArenaOutline)
        {
            // This is the arena bounds identical to what is drawn in radar.
            Camera.Instance?.DrawWorldPoly(_cameraCenter, _bounds.Shape, Colors.Border, _projectedThickness);
            // This is an attempt grab all the other items in the drawlist in hopes of seeing aoe drawn on the arena also. Doesn't work. The camera doesn't handle stencils it seems.
            //Camera.Instance?.DrawWorldPrimitives(_cameraCenter, drawList);
            //    public static void AppendRelPoly(RelSimplifiedComplexPolygon polygon, uint color)
            //Dx11ArenaRenderer.AppendRelPoly(_bounds.Shape, Colors.Border);

        }
        Dx11ArenaRenderer.BeginArena(drawList, _bounds.Shape, centerX, centerY, _scaledCos, _scaledSin, screenScale);
        // This is now pegged to (0,0) display coordinates (top left corner) but it does render the little radar along with rotating it.
        // Maybe I can feed it through Camera to git it aligned with the view and angle of the playing field? Also the text hints
        // still have their own quite large window floating in front of everything where radar used to be, just the radar isn't in same
        // window. It does look like it moves up and down if there are text hints. Probably need to just adjust the offset based off of display logic.
        // ie center should offset to be around 1920, 1080. Yes! , _center.X + 1920, _center.Z + 1080, does offset floating radar to be directly in
        // middle of display
        // TODO 1: figure out how to get this to angle to the same plane as camera view.
        // TODO 2: Scale it to be same size as arena -> can probably just cheat off os arena scale logic really?
        // TODO turn this off if you do not want radar floating in center.
        //Dx11ArenaRenderer.BeginArena(projectedDrawList, _bounds.Shape, _center.X + displayOffsetX, _center.Z + displayOffsetY, _scaledCos, _scaledSin, displayScreenScale);
        // This is treating the whole screen as a 2d window pane instead of a 3 dimensional field.
        //Dx11ArenaRenderer.BeginArena(projectedDrawList, _bounds.Shape, _center.X + displayOffsetX, _center.Z + displayOffsetY, 12, 12, 12);




        // This being off means you don't have the black background inside arena bounds.
        if (Config.OpaqueArenaBackground)
        {
            Dx11ArenaRenderer.AppendArenaBackground(Colors.Background);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 WorldPositionToScreenPosition(WPos p) => ScreenCenter + WorldOffsetToScreenOffset(p - new WPos(_cameraCenter.XZ()));
    //public Vector2 WorldPositionToScreenPosition(WPos p) => _cameraCenter.XZ() + WorldOffsetToScreenOffset(p - _center);
    public Vector2 WorldPositionToScreenPosition(Vector3 worldPos, Matrix4x4 viewProjectionMatrix)
    {
        var screenPos = Vector2.Zero;
        Vector4 clipSpace = Vector4.Transform(new Vector4(worldPos, 1.0f) , viewProjectionMatrix);

        if (clipSpace.W < 0.1f) return new Vector2(0,0);

        Vector3 ndc = new Vector3(clipSpace.X, clipSpace.Y, clipSpace.Z) / clipSpace.W;

        Vector2 windowPos = ImGui.GetMainViewport().Pos;
        Vector2 windowSize = ImGui.GetMainViewport().Size;

        screenPos.X = windowPos.X + ((ndc.X + 1.0f) * 0.5f * windowSize.X);
        screenPos.Y = windowPos.Y + (ndc.Y + 1.0f) * 0.5f * windowSize.Y;

        return screenPos;
    }



    // this is useful for drawing on margins (TODO better api)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 RotatedCoords(Vector2 coords)
    {
        var cx = coords.X;
        var cy = coords.Y;
        var x = cx * _cameraCosAzimuth - cy * _cameraSinAzimuth;
        var y = cy * _cameraCosAzimuth + cx * _cameraSinAzimuth;
        return new(x, y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector2 WorldOffsetToScreenOffset(WDir worldOffset)
    {
        var wx = worldOffset.X;
        var wz = worldOffset.Z;
        return new(wx * _scaledCos - wz * _scaledSin, wz * _scaledCos + wx * _scaledSin);
    }

    // Unclipped primitive rendering that accepts world-space positions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddLine(WPos a, WPos b, uint color = default, float thickness = 1f)
    {
        PrepareOutlineStyle(color, thickness, out var lineColor, out var lineThickness, out var shadowColor, out var shadowThickness);
        Span<WDir> points = [a - _center, b - _center];
        Dx11ArenaRenderer.AppendPolyline(points, false, lineColor, lineThickness, shadowColor, shadowThickness);

        // This will just be outlines for now.
        if (Config.ShowWorldArenaAOEOutlines)
        {
            //Circle zoneCircle = new Circle(center, radius);
            //Camera.Instance?.DrawWorldLineDirs(_cameraCenter, points, color != default ? color : Colors.AOE, _projectdThickness);
            Camera.Instance?.DrawWorldLine(a.ToVec3(_cameraCenter.Y), b.ToVec3(_cameraCenter.Y), color != default ? color : Colors.AOE, _projectedThickness);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddCircleUnfilled(WPos center, float radius, uint color = default, float thickness = 1f)
    {
        PrepareOutlineStyle(color, thickness, out var lineColor, out var lineThickness, out var shadowColor, out var shadowThickness);
        Dx11ArenaRenderer.AppendCircleOutlineUnclipped(center - _center, radius, lineColor, lineThickness, shadowColor, shadowThickness);

        if (Config.ShowWorldArenaAOEOutlines)
        {
            Circle zoneCircle = new Circle(center, radius);
            Camera.Instance?.DrawWorldShape(_cameraCenter, zoneCircle, color != default ? color : Colors.AOE, _projectedThickness);
        }
    }

    // Looks like this is used for enemy and player triangles. If it is only those then we can pin projection to whether or not we see those.
    // TODO they only show up black instead of the color codes from radar.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddTriangle(WPos p1, WPos p2, WPos p3, uint color = default, float thickness = 1f)
    {
        Dx11ArenaRenderer.AppendPrimitiveTriangleStroke(p1 - _center, p2 - _center, p3 - _center, color != default ? color : Colors.Danger, thickness * _frameThicknessScale);

        if (Config.ShowWorldArenaAOEOutlines)
        {
            //Circle zoneCircle = new Circle(center, radius);
            //Camera.Instance?.DrawWorldShape(_cameraCenter, zoneCircle, color != default ? color : Colors.AOE, _projectdThickness);
            Camera.Instance?.DrawWorldLine(p1.ToVec3(_cameraCenter.Y), p2.ToVec3(_cameraCenter.Y), color, _projectedThickness);
            Camera.Instance?.DrawWorldLine(p2.ToVec3(_cameraCenter.Y), p3.ToVec3(_cameraCenter.Y), color, _projectedThickness);
            Camera.Instance?.DrawWorldLine(p3.ToVec3(_cameraCenter.Y), p1.ToVec3(_cameraCenter.Y), color, _projectedThickness);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddTriangleFilled(WPos p1, WPos p2, WPos p3, uint color = default)
        => Dx11ArenaRenderer.AppendPrimitiveTriangle(p1 - _center, p2 - _center, p3 - _center, color != default ? color : Colors.Danger);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddQuad(WPos p1, WPos p2, WPos p3, WPos p4, uint color = default, float thickness = 1f)
    {
        Dx11ArenaRenderer.AppendQuadStroke(p1 - _center, p2 - _center, p3 - _center, p4 - _center, color != default ? color : Colors.Danger, thickness * _frameThicknessScale);
        if (Config.ShowWorldArenaAOEOutlines)
        {
            Camera.Instance?.DrawWorldLine(p1.ToVec3(_cameraCenter.Y), p2.ToVec3(_cameraCenter.Y), color != default ? color : Colors.AOE, _projectedThickness);
            Camera.Instance?.DrawWorldLine(p2.ToVec3(_cameraCenter.Y), p3.ToVec3(_cameraCenter.Y), color != default ? color : Colors.AOE, _projectedThickness);
            Camera.Instance?.DrawWorldLine(p3.ToVec3(_cameraCenter.Y), p4.ToVec3(_cameraCenter.Y), color != default ? color : Colors.AOE, _projectedThickness);
            Camera.Instance?.DrawWorldLine(p4.ToVec3(_cameraCenter.Y), p1.ToVec3(_cameraCenter.Y), color != default ? color : Colors.AOE, _projectedThickness);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddRect(WPos origin, WDir direction, float lenFront, float lenBack, float halfWidth, uint color, float thickness = 1f)
    {
        thickness *= _frameThicknessScale;
        var side = halfWidth * direction.OrthoR();
        var front = origin + lenFront * direction;
        var back = origin - lenBack * direction;
        AddQuad(front + side, front - side, back - side, back + side, color, thickness);

        if (Config.ShowWorldArenaAOEOutlines)
        {
            //var rotation = (float)Math.Atan2(direction.X, direction.Z);
            //var wpos2 = lenFront * direction.OrthoR();
            //RectangleSE rectangleSE = new RectangleSE(origin, new WPos(wpos2.X, wpos2.Z), halfWidth);
            var frontCamera = origin - (lenFront/2) * direction;
            var backCamera = origin + (lenBack) * direction;
            RectangleSE rectangleSE = new RectangleSE(front, back, halfWidth);
            //Rectangle rectangle = new Rectangle(origin, halfWidth, lenFront / 2, rotation.Degrees()); //todo rectangle
            //            Camera.Instance?.DrawWorldShape(_cameraCenter, zoneCircle, color != default ? color : Colors.AOE, _projectdThickness);

            //Camera.Instance?.DrawWorldRectangle(_cameraCenter, rectangleSE, color != default ? color : Colors.AOE, _projectedThickness);
            //Camera.Instance?.DrawWorldShape(_cameraCenter, rectangleSE, color != default ? color : Colors.AOE, _projectedThickness);
            // is this m3 tyrant arena split?

        }

    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddPolygon(ReadOnlySpan<WPos> vertices, uint color = default, float thickness = 1f)
    {
        var len = vertices.Length;
        Span<WDir> local = stackalloc WDir[len];
        for (var i = 0; i < len; ++i)
        {
            local[i] = vertices[i] - _center;
        }
        Dx11ArenaRenderer.AppendPolyline(local, true, color != default ? color : Colors.Danger, thickness * _frameThicknessScale);

        if (Config.ShowWorldArenaAOEOutlines)
        {
            var length = vertices.Length;
            var wDirs = new WDir[length];
            for (var i = 0; i < length; ++i)
            {
                // World shape can take world coordinates, they are adjusted in camera
                wDirs[i] = vertices[i].ToWDir();
            }
            Camera.Instance?.DrawWorldLineDirs(_cameraCenter, wDirs, color != default ? color : Colors.AOE, _projectedThickness);
        }
    }

    public void AddComplexPolygon(RelSimplifiedComplexPolygon poly, uint color = default, float thickness = 1f)
    {
        var parts = CollectionsMarshal.AsSpan(poly.Parts);
        var len = parts.Length;
        PrepareOutlineStyle(color, thickness, out var lineColor, out var lineThickness, out var shadowColor, out var shadowThickness);

        for (var i = 0; i < len; ++i)
        {
            var part = parts[i];
            DrawContour(part.Exterior);
            var countH = part.HoleStarts.Count;
            for (var h = 0; h < countH; ++h)
            {
                DrawContour(part.Interior(h));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void DrawContour(ReadOnlySpan<WDir> contour)
            => Dx11ArenaRenderer.AppendPolyline(contour, true, lineColor, lineThickness, shadowColor, shadowThickness);

        if (Config.ShowWorldArenaAOEOutlines)
        {
            Camera.Instance?.DrawWorldPoly(_cameraCenter, poly, color != default ? color : Colors.AOE, _projectedThickness);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PathLineTo(WPos p) => Dx11ArenaRenderer.PathLineTo(p - _center);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PathArcTo(WPos center, float radius, float amin, float amax) => Dx11ArenaRenderer.PathArcTo(center - _center, radius, amin, amax);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PathStroke(bool closed, uint color = default, float thickness = 1f)
        => Dx11ArenaRenderer.PathStroke(closed, color != default ? color : Colors.Danger, thickness * Config.ThicknessScale);

    // Filled zones:
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //public void ZoneCone(WPos center, float innerRadius, float outerRadius, Angle centerDirection, Angle halfAngle, uint color) => Dx11ArenaRenderer.AppendCone(center - _center, innerRadius, outerRadius, centerDirection.ToDirection(), halfAngle.Rad, color != default ? color : Colors.AOE);
    public void ZoneCone(WPos center, float innerRadius, float outerRadius, Angle centerDirection, Angle halfAngle, uint color)
    {
        Dx11ArenaRenderer.AppendCone(center - _center, innerRadius, outerRadius, centerDirection.ToDirection(), halfAngle.Rad, color != default ? color : Colors.AOE);
        // This will just be outlines.
        if (Config.ShowWorldArenaAOEOutlines)
        {
            //TODO needs verification
            DonutSegmentHA zoneCone = new DonutSegmentHA(center, innerRadius, outerRadius, centerDirection, halfAngle);
            Camera.Instance!.DrawWorldShape(_cameraCenter, zoneCone, Colors.Danger, _projectedThickness);
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //public void ZoneCircle(WPos center, float radius, uint color) => Dx11ArenaRenderer.AppendCircle(center - _center, radius, color != default ? color : Colors.AOE);
    public void ZoneCircle(WPos center, float radius, uint color)
    {
        Dx11ArenaRenderer.AppendCircle(center - _center, radius, color != default ? color : Colors.AOE);
        // only draws an outline
        if (Config.ShowWorldArenaAOEOutlines)
        {
            Circle zoneCircle = new Circle(center, radius);
            Camera.Instance?.DrawWorldShape(_cameraCenter, zoneCircle, color != default ? color : Colors.AOE, _projectedThickness);
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //public void ZoneDonut(WPos center, float innerRadius, float outerRadius, uint color) => Dx11ArenaRenderer.AppendDonut(center - _center, innerRadius, outerRadius, color != default ? color : Colors.AOE);
    public void ZoneDonut(WPos center, float innerRadius, float outerRadius, uint color)
    {
        Dx11ArenaRenderer.AppendDonut(center - _center, innerRadius, outerRadius,
            color != default ? color : Colors.AOE);
        // This will just be outlines for now.
        if (Config.ShowWorldArenaAOEOutlines)
        {
            //TODO thickness of these should probably be button someplace
            Donut zoneDonut = new Donut(center, innerRadius, outerRadius);
            Camera.Instance?.DrawWorldShape(_cameraCenter, zoneDonut, color != default ? color : Colors.AOE, _projectedThickness);
            //public sealed class Donut(WPos center, float innerRadius, float outerRadius) : Shape
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //public void ZoneTri(WPos a, WPos b, WPos c, uint color) => Dx11ArenaRenderer.AppendTriangle(a - _center, b - _center, c - _center, color != default ? color : Colors.AOE);
    public void ZoneTri(WPos a, WPos b, WPos c, uint color)
    {
        Dx11ArenaRenderer.AppendTriangle(a - _center, b - _center, c - _center, color != default ? color : Colors.AOE);
        // This will just be outlines for now.
        if (Config.ShowWorldArenaAOEOutlines)
        {
            //Circle zoneCircle = new Circle(center, radius);
            //Camera.Instance?.DrawWorldShape(_cameraCenter, zoneCircle, color != default ? color : Colors.AOE, _projectdThickness);
            Camera.Instance?.DrawWorldLine(a.ToVec3(_cameraCenter.Y), b.ToVec3(_cameraCenter.Y), color != default ? color : Colors.AOE, _projectedThickness);
            Camera.Instance?.DrawWorldLine(b.ToVec3(_cameraCenter.Y), c.ToVec3(_cameraCenter.Y), color != default ? color : Colors.AOE, _projectedThickness);
            Camera.Instance?.DrawWorldLine(c.ToVec3(_cameraCenter.Y), a.ToVec3(_cameraCenter.Y), color != default ? color : Colors.AOE, _projectedThickness);
        }

    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ZoneIsoscelesTri(WPos apex, WDir height, WDir halfBase, uint color)
    {
        var a = apex - _center;
        Dx11ArenaRenderer.AppendTriangle(a, a + height + halfBase, a + height - halfBase, color != default ? color : Colors.AOE);
        // This will just be outlines for now.
        if (Config.ShowWorldArenaAOEOutlines)
        {
            var apexDir = apex.ToWDir();
            WDir[] wDirs = [apexDir, (a + height + halfBase), (a + height - halfBase)];

            Camera.Instance?.DrawWorldLineDirs(_cameraCenter, wDirs, color != default ? color : Colors.AOE, _projectedThickness);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ZoneIsoscelesTri(WPos apex, Angle direction, Angle halfAngle, float height, uint color)
    {
        var a = apex - _center;
        var dir = direction.ToDirection();
        var h = height * dir;
        var halfBase = height * halfAngle.Tan() * dir.OrthoL();
        Dx11ArenaRenderer.AppendTriangle(a, a + h + halfBase, a + h - halfBase, color != default ? color : Colors.AOE);
        // This will just be outlines for now.
        if (Config.ShowWorldArenaAOEOutlines)
        {
            var apexDir = apex.ToWDir();
            WDir[] wDirs = [apexDir, apexDir + h + halfBase, apexDir + h - halfBase];

            Camera.Instance?.DrawWorldLineDirs(_cameraCenter, wDirs, color != default ? color : Colors.AOE, _projectedThickness);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //public void ZoneRect(WPos origin, WDir direction, float lenFront, float lenBack, float halfWidth, uint color) => Dx11ArenaRenderer.AppendRect(origin - _center, direction, lenFront, lenBack, halfWidth, color != default ? color : Colors.AOE);
    public void ZoneRect(WPos origin, WDir direction, float lenFront, float lenBack, float halfWidth, uint color)
    {
        Dx11ArenaRenderer.AppendRect(origin - _center, direction, lenFront, lenBack, halfWidth, color != default ? color : Colors.AOE);

        // Just shows outlines
        if (Config.ShowWorldArenaAOEOutlines)
        {
            var front = origin - (lenFront/2) * direction;
            var back = origin + (lenBack) * direction;
            RectangleSE rectangleSE = new RectangleSE(front, back, halfWidth);
            //var front = origin + lenFront * direction;
            //var back = origin - lenBack * direction;
            //RectangleSE rectangleSE = new RectangleSE(front, back, halfWidth);
            //Camera.Instance?.DrawWorldShape(_cameraCenter, rectangleSE, color != default ? color : Colors.AOE, _projectedThickness);
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //public void ZoneRect(WPos origin, Angle direction, float lenFront, float lenBack, float halfWidth, uint color) => Dx11ArenaRenderer.AppendRect(origin - _center, direction.ToDirection(), lenFront, lenBack, halfWidth, color != default ? color : Colors.AOE);
    public void ZoneRect(WPos origin, Angle direction, float lenFront, float lenBack, float halfWidth, uint color)
    {
        Dx11ArenaRenderer.AppendRect(origin - _center, direction.ToDirection(), lenFront, lenBack, halfWidth,
            color != default ? color : Colors.AOE);
        // Just shows outlines
        if (Config.ShowWorldArenaAOEOutlines)
        {
            // Convert angle to WDir
            /*float x = MathF.Cos(direction.Rad);
            float y = MathF.Sin(direction.Rad);
            var wDirection = new WDir(x, y);*/


            // TODO verify this works all over
            var originCenter = new WPos((origin.X + lenFront / 2), origin.Z);
            // The intent is to move the origin into the middle of the lenFront.
            //var originCenter = new WPos((origin.X + lenFront/2), origin.Z);
            var directionDir = direction.ToDirection();
            RectangleSE rectangleSe = new RectangleSE(origin, originCenter, halfWidth);
            // TODO need to come up with the offset for these rectangles so that the center is in center of rectangle instead of the origin cast location.
            var wPosOffset = (origin.ToVec2() + _cameraCenter.XZ());
            var halfFront = (lenFront / 2);// * direction;
            //var posOffsetX = origin.X + halfWidth;
            var posOffsetZ = origin.Z - halfFront;
            var posOffset = new WPos(origin.X, posOffsetZ);
            /*
             * Given a Vector 2 coordinate and an angle of travel we want to move a line segment back half the length of the line on a grid.
             * We need to take the radians and coordinate the forward direction vector
             * then move the line back by (originalPos - (forwardDirection * segmentLength))
             */
            WDir forwardDirection = new WDir(MathF.Sin(direction.Rad), MathF.Cos(direction.Rad));
            WPos originOffset = origin + (forwardDirection * (lenFront / 2f));

            Rectangle rectangle = new Rectangle(originOffset, halfWidth, lenFront/2, direction);
            // This is lined up on center but still going to the from center to left.
            Camera.Instance?.DrawWorldShape(_cameraCenter, rectangle, color != default ? color : Colors.AOE, _projectedThickness);
            //Camera.Instance?.DrawWorldShape(_cameraCenter, rectangleSe, color != default ? color : Colors.AOE, _projectedThickness);
            //if (lenBack <= 0 || lenBack == default)
            //{
                //Rectangle rectangle = new Rectangle(origin, halfWidth, (lenFront / 2), direction);
                // This is lined up on center but still going to the from center to left.
                //Camera.Instance?.DrawWorldRectangle(_cameraCenter, rectangle, color, _projectedThickness);
                // TODO: This is the one not behaving on siren
                // Is this tyrant? ~ yes, this is where it gets weird on m3 tyrant
            //}
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ZoneRect(WPos start, WPos end, float halfWidth, uint color)
    {
        var dir = end - start;
        var len = dir.Length();
        if (len > 0f)
        {
            Dx11ArenaRenderer.AppendRect(start - _center, dir / len, len, 0f, halfWidth, color != default ? color : Colors.AOE);
        }
        // Just shows outlines
        if (Config.ShowWorldArenaAOEOutlines)
        {
            RectangleSE rectangleSE = new RectangleSE(start, end, halfWidth);
            Camera.Instance?.DrawWorldShape(_cameraCenter, rectangleSE, color != default ? color : Colors.AOE, _projectedThickness);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //public void ZoneCross(WPos origin, Angle rotation, float range, float halfWidth, uint color) => Dx11ArenaRenderer.AppendCross(origin - _center, rotation.ToDirection(), range, halfWidth, color != default ? color : Colors.AOE);
    public void ZoneCross(WPos origin, Angle rotation, float range, float halfWidth, uint color)
    {
        Dx11ArenaRenderer.AppendCross(origin - _center, rotation.ToDirection(), range, halfWidth, color != default ? color : Colors.AOE);
        // Just shows outlines
        if (Config.ShowWorldArenaAOEOutlines)
        {
            var cross = new Cross(origin, range, halfWidth, rotation);
            Camera.Instance!.DrawWorldShape(_cameraCenter, cross, color != default ? color : Colors.AOE, _projectedThickness);
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //public void ZoneRelPoly(RelSimplifiedComplexPolygon poly, uint color) => Dx11ArenaRenderer.AppendRelPoly(poly, color != default ? color : Colors.AOE);
    public void ZoneRelPoly(RelSimplifiedComplexPolygon poly, uint color)
    {
        Dx11ArenaRenderer.AppendRelPoly(poly, color != default ? color : Colors.AOE);
        // Just shows outlines
        if (Config.ShowWorldArenaAOEOutlines)
        {
            Camera.Instance!.DrawWorldPoly(_cameraCenter, poly, color != default ? color : Colors.AOE, _projectedThickness);
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //public void ZoneCapsule(WPos start, WDir direction, float radius, float length, uint color) => Dx11ArenaRenderer.AppendCapsule(start - _center, direction, radius, length, color != default ? color : Colors.AOE);
    public void ZoneCapsule(WPos start, WDir direction, float radius, float length, uint color)
    {
        Dx11ArenaRenderer.AppendCapsule(start - _center, direction, radius, length, color != default ? color : Colors.AOE);

        // Just shows outlines
        if (Config.ShowWorldArenaAOEOutlines)
        {
            var rotation = (float)Math.Atan2(direction.Z, direction.X);
            var rotAngle = rotation;
            var capsule = new Capsule(start, radius, length, default, rotation.Degrees());
            Camera.Instance!.DrawWorldShape(_cameraCenter, capsule, color != default ? color : Colors.AOE, _projectedThickness);
        }
    }


    // TODO punt on this one for now.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ZoneArcCapsule(WPos start, WPos orbitCenter, Angle angularLength, float radius, uint color)
        => Dx11ArenaRenderer.AppendArcCapsule(start - _center, orbitCenter - start, angularLength.Rad, radius, color != default ? color : Colors.AOE);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PrepareOutlineStyle(uint color, float thickness, out uint lineColor, out float lineThickness, out uint shadowColor, out float shadowThickness)
    {
        lineColor = color != default ? color : Colors.Danger;
        lineThickness = thickness * _frameThicknessScale;
        if (_frameShowOutlinesAndShadows)
        {
            shadowColor = Colors.Shadows;
            shadowThickness = (thickness + 1f) * _frameThicknessScale;
        }
        else
        {
            shadowColor = 0u;
            shadowThickness = lineThickness;
        }
    }

    // draw zone outlines
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ZoneConeOutline(WPos center, float innerRadius, float outerRadius, Angle centerDirection, Angle halfAngle, uint color = default, float thickness = 1f)
    {
        PrepareOutlineStyle(color, thickness, out var lineColor, out var lineThickness, out var shadowColor, out var shadowThickness);
        Dx11ArenaRenderer.AppendConeOutline(center - _center, innerRadius, outerRadius, centerDirection.ToDirection(), halfAngle.Rad, lineColor, lineThickness, shadowColor, shadowThickness);

        // This will just be outlines.
        if (Config.ShowWorldArenaAOEOutlines)
        {
            //TODO needs verification
            DonutSegmentHA zoneCone = new DonutSegmentHA(center, innerRadius, outerRadius, centerDirection, halfAngle);
            Camera.Instance!.DrawWorldShape(_cameraCenter, zoneCone, Colors.Danger, _projectedThickness);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ZoneCircleOutline(WPos center, float radius, uint color = default, float thickness = 1f)
    {
        PrepareOutlineStyle(color, thickness, out var lineColor, out var lineThickness, out var shadowColor, out var shadowThickness);
        Dx11ArenaRenderer.AppendCircleOutline(center - _center, radius, lineColor, lineThickness, shadowColor, shadowThickness);

        // only draws an outline
        if (Config.ShowWorldArenaAOEOutlines)
        {
            Circle zoneCircle = new Circle(center, radius);
            Camera.Instance?.DrawWorldShape(_cameraCenter, zoneCircle, color != default ? color : Colors.AOE, _projectedThickness);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ZoneDonutOutline(WPos center, float innerRadius, float outerRadius, uint color = default, float thickness = 1f)
    {
        PrepareOutlineStyle(color, thickness, out var lineColor, out var lineThickness, out var shadowColor, out var shadowThickness);
        Dx11ArenaRenderer.AppendDonutOutline(center - _center, innerRadius, outerRadius, lineColor, lineThickness, shadowColor, shadowThickness);
        // This will just be outlines for now.
        if (Config.ShowWorldArenaAOEOutlines)
        {
            //TODO thickness of these should probably be button someplace
            Donut zoneDonut = new Donut(center, innerRadius, outerRadius);
            Camera.Instance?.DrawWorldShape(_cameraCenter, zoneDonut, color != default ? color : Colors.AOE, _projectedThickness);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ZoneTriOutline(WPos a, WPos b, WPos c, uint color = default, float thickness = 1f)
    {
        PrepareOutlineStyle(color, thickness, out var lineColor, out var lineThickness, out var shadowColor, out var shadowThickness);
        Dx11ArenaRenderer.AppendTriangleOutline(a - _center, b - _center, c - _center, lineColor, lineThickness, shadowColor, shadowThickness);

        // This will just be outlines for now.
        if (Config.ShowWorldArenaAOEOutlines)
        {
            Camera.Instance?.DrawWorldLine(a.ToVec3(_cameraCenter.Y), b.ToVec3(_cameraCenter.Y), color != default ? color : Colors.AOE, _projectedThickness);
            Camera.Instance?.DrawWorldLine(b.ToVec3(_cameraCenter.Y), c.ToVec3(_cameraCenter.Y), color != default ? color : Colors.AOE, _projectedThickness);
            Camera.Instance?.DrawWorldLine(c.ToVec3(_cameraCenter.Y), a.ToVec3(_cameraCenter.Y), color != default ? color : Colors.AOE, _projectedThickness);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ZoneIsoscelesTriOutline(WPos apex, WDir height, WDir halfBase, uint color = default, float thickness = 1f)
    {
        var a = apex - _center;
        PrepareOutlineStyle(color, thickness, out var lineColor, out var lineThickness, out var shadowColor, out var shadowThickness);
        Dx11ArenaRenderer.AppendTriangleOutline(a, a + height + halfBase, a + height - halfBase, lineColor, lineThickness, shadowColor, shadowThickness);
        // This will just be outlines for now.
        if (Config.ShowWorldArenaAOEOutlines)
        {
            var apexDir = apex.ToWDir();
            WDir[] wDirs = [apexDir, (a + height + halfBase), (a + height - halfBase)];

            Camera.Instance?.DrawWorldLineDirs(_cameraCenter, wDirs, color != default ? color : Colors.AOE, _projectedThickness);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ZoneRectOutline(WPos origin, WDir direction, float lenFront, float lenBack, float halfWidth, uint color = default, float thickness = 1f)
    {
        PrepareOutlineStyle(color, thickness, out var lineColor, out var lineThickness, out var shadowColor, out var shadowThickness);
        Dx11ArenaRenderer.AppendRectOutline(origin - _center, direction, lenFront, lenBack, halfWidth, lineColor, lineThickness, shadowColor, shadowThickness);

        // Just shows outlines
        if (Config.ShowWorldArenaAOEOutlines)
        {
            var front = origin - (lenFront/2) * direction;
            var back = origin + (lenBack) * direction;
            RectangleSE rectangleSE = new RectangleSE(front, back, halfWidth);
            //Camera.Instance?.DrawWorldShape(_cameraCenter, rectangleSE, color != default ? color : Colors.AOE, _projectedThickness);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ZoneRectOutline(WPos origin, Angle direction, float lenFront, float lenBack, float halfWidth, uint color = default, float thickness = 1f)
    {
        PrepareOutlineStyle(color, thickness, out var lineColor, out var lineThickness, out var shadowColor, out var shadowThickness);
        Dx11ArenaRenderer.AppendRectOutline(origin - _center, direction.ToDirection(), lenFront, lenBack, halfWidth, lineColor, lineThickness, shadowColor, shadowThickness);
        // Just shows outlines
        if (Config.ShowWorldArenaAOEOutlines)
        {
            // TODO verify this works all over.
            // TODO if lenBack != default or 0 we need to account for that with RectangleSE probably. also add that logic to ZoneRectFilled with this same method signature.
            // The intent is to move the origin into the middle of the lenFront.
            var originCenter = new WPos((origin.X + lenFront/2), origin.Z);
            var directionDir = direction.ToDirection();
            RectangleSE rectangleSe = new RectangleSE(origin, originCenter, halfWidth);
            Rectangle rectangle = new Rectangle(origin, halfWidth, lenFront/2, direction);
            // This is lined up on center but still going to the from center to left.
            //Camera.Instance?.DrawWorldShape(_cameraCenter, rectangle, color != default ? color : Colors.AOE, _projectedThickness);
            //Camera.Instance?.DrawWorldShape(_cameraCenter, rectangleSe, color != default ? color : Colors.AOE, _projectedThickness);
            // TODO: This is the one not behaving on siren
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ZoneRectOutline(WPos start, WPos end, float halfWidth, uint color = default, float thickness = 1f)
    {
        var dir = end - start;
        var len = dir.Length();
        if (!(len > 0f))
        {
            return;
        }
        PrepareOutlineStyle(color, thickness, out var lineColor, out var lineThickness, out var shadowColor, out var shadowThickness);
        Dx11ArenaRenderer.AppendRectOutline(start - _center, dir / len, len, 0f, halfWidth, lineColor, lineThickness, shadowColor, shadowThickness);

        // Just shows outlines
        if (Config.ShowWorldArenaAOEOutlines)
        {
            //arena.ZoneRectOutline(origin, (rotation + DirectionOffset).ToDirection(), LengthFront, LengthBack, HalfWidth, color, thickness)
            RectangleSE rectangleSE = new RectangleSE(start, end, halfWidth);
            Camera.Instance?.DrawWorldShape(_cameraCenter, rectangleSE, color != default ? color : Colors.AOE, _projectedThickness);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ZoneCrossOutline(WPos origin, Angle rotation, float range, float halfWidth, uint color = default, float thickness = 1f)
    {
        PrepareOutlineStyle(color, thickness, out var lineColor, out var lineThickness, out var shadowColor, out var shadowThickness);
        Dx11ArenaRenderer.AppendCrossOutline(origin - _center, rotation.ToDirection(), range, halfWidth, lineColor, lineThickness, shadowColor, shadowThickness);

        // Just shows outlines
        if (Config.ShowWorldArenaAOEOutlines)
        {
            var cross = new Cross(origin, range, halfWidth, rotation);
            Camera.Instance!.DrawWorldShape(_cameraCenter, cross, color != default ? color : Colors.AOE, _projectedThickness);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ZoneRelPolyOutline(RelSimplifiedComplexPolygon poly, uint color = default, float thickness = 1f)
    {
        PrepareOutlineStyle(color, thickness, out var lineColor, out var lineThickness, out var shadowColor, out var shadowThickness);
        Dx11ArenaRenderer.AppendCustomOutline(poly, lineColor, lineThickness, shadowColor, shadowThickness);
        // Just shows outlines
        if (Config.ShowWorldArenaAOEOutlines)
        {
            Camera.Instance!.DrawWorldPoly(_cameraCenter, poly, color != default ? color : Colors.AOE, _projectedThickness);
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ZoneCapsuleOutline(WPos start, WDir direction, float radius, float length, uint color = default, float thickness = 1f)
    {
        PrepareOutlineStyle(color, thickness, out var lineColor, out var lineThickness, out var shadowColor, out var shadowThickness);
        Dx11ArenaRenderer.AppendCapsuleOutline(start - _center, direction, radius, length, lineColor, lineThickness, shadowColor, shadowThickness);

        // Just shows outlines
        if (Config.ShowWorldArenaAOEOutlines)
        {
            var rotation = (float)Math.Atan2(direction.Z, direction.X);
            var rotAngle = rotation;
            var capsule = new Capsule(start, radius, length, default, rotation.Degrees());
            Camera.Instance!.DrawWorldShape(_cameraCenter, capsule, color != default ? color : Colors.AOE, _projectedThickness);
        }
    }

    // TODO punt on ArcCapsules for now
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ZoneArcCapsuleOutline(WPos start, WPos orbitCenter, Angle angularLength, float radius, uint color = default, float thickness = 1f)
    {
        PrepareOutlineStyle(color, thickness, out var lineColor, out var lineThickness, out var shadowColor, out var shadowThickness);
        Dx11ArenaRenderer.AppendArcCapsuleOutline(start - _center, orbitCenter - start, angularLength.Rad, radius, lineColor, lineThickness, shadowColor, shadowThickness);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SpriteScreen(Vector2 min, Vector2 max, IDalamudTextureWrap texture, uint color = 0xFFFFFFFFu)
        => Dx11ArenaRenderer.AppendSpriteScreen(min, max, texture, color);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TextScreen(Vector2 center, string text, uint color, float fontSize = 17f, uint outlineColor = 0u, float outlineWidth = 0f)
        => Dx11ArenaRenderer.AppendTextScreen(center, text, fontSize * _frameArenaScale, color, outlineColor, outlineWidth * _frameArenaScale);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TextWorld(WPos center, string text, uint color, float fontSize = 17f, uint outlineColor = 0u, float outlineWidth = 0f)
        => TextScreen(WorldPositionToScreenPosition(new Vector3(center.X, _cameraCenter.Y, center.Z), Camera.Instance.ViewProj), text, color, fontSize, outlineColor, outlineWidth);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IconScreen(Vector2 center, FontAwesomeIcon icon, uint color, float fontSize = 17f)
    {
        var text = icon.ToIconString();
        Dx11ArenaRenderer.AppendIconScreen(center, text, fontSize, color);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //public void IconWorld(WPos center, FontAwesomeIcon icon, uint color, float fontSize = 17f) => IconScreesn(WorldPositionToScreenPosition(center), icon, color, fontSize);
    public void IconWorld(WPos center, FontAwesomeIcon icon, uint color, float fontSize = 17f) => IconScreen(WorldPositionToScreenPosition(new Vector3(center.X, _cameraCenter.Y, center.Z), Camera.Instance.ViewProj), icon, color, fontSize);


    public void CardinalNames()
    {
        var center = ScreenCenter;
        var fontSetting = _frameCardinalsFontSize;
        var offCenterSizeOffset = (_frameScreenHalfSize + _frameScreenMarginSize * 0.5f) * _bounds.ScaleFactor + fontSetting - 17f;
        var offS = RotatedCoords(new(default, offCenterSizeOffset));
        var offE = RotatedCoords(new(offCenterSizeOffset, default));
        TextScreen(center - offS, "N", Colors.CardinalN, fontSetting);
        TextScreen(center + offS, "S", Colors.CardinalS, fontSetting);
        TextScreen(center + offE, "E", Colors.CardinalE, fontSetting);
        TextScreen(center - offE * 1.02f, "W", Colors.CardinalW, fontSetting); // w is slightly wider, so we are putting it 2% farther away than the E
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ActorInsideBounds(WPos position, Angle rotation, uint color)
    {
        var scale = _frameActorScale * _frameThicknessScale;
        var dir = rotation.ToDirection();
        var scale07 = scale * 0.7f * dir;
        var scale035 = scale * 0.35f * dir;
        var scale0433 = scale * 0.433f * dir.OrthoR();
        var positionscale07 = position + scale07;
        var positionscale035 = position - scale035;
        var positionscale035pscale0433 = positionscale035 + scale0433;
        var positionscale035mscale0433 = positionscale035 - scale0433;
        if (_frameShowOutlinesAndShadows)
        {
            AddTriangle(positionscale07, positionscale035pscale0433, positionscale035mscale0433, Colors.Shadows, 2f);
        }

        AddTriangleFilled(positionscale07, positionscale035pscale0433, positionscale035mscale0433, color);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ActorOutsideBounds(WPos position, Angle rotation, uint color)
    {
        var scale = _frameActorScale;
        var dir = rotation.ToDirection();
        var scale07 = scale * 0.7f * dir;
        var scale035 = scale * 0.35f * dir;
        var scale0433 = scale * 0.433f * dir.OrthoR();
        var positionscale035 = position - scale035;
        AddTriangle(position + scale07, positionscale035 + scale0433, positionscale035 - scale0433, color);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ActorProjected(WPos from, WPos to, Angle rotation, uint color)
    {
        if (InBounds(to))
        {
            // projected position is inside bounds
            ActorInsideBounds(to, rotation, color);
            return;
        }

        var dir = to - from;
        var l = dir.Length();

        if (l == default)
        {
            return; // can't determine projection direction
        }

        dir /= l;
        var t = IntersectRayBounds(from, dir);
        if (t <= l)
        {
            ActorOutsideBounds(from + t * dir, rotation, color);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Actor(WPos position, Angle rotation, uint color)
    {
        if (InBounds(position))
        {
            ActorInsideBounds(position, rotation, color);
        }
        else
        {
            ActorOutsideBounds(ClampToBounds(position), rotation, color);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Actor(Actor? actor, uint color = default, bool allowDeadAndUntargetable = false)
    {
        if (actor != null && !actor.IsDestroyed && (allowDeadAndUntargetable || actor.IsTargetable && !actor.IsDead))
        {
            Actor(actor.Position, actor.Rotation, color == default ? Colors.Enemy : color);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Actors(IEnumerable<Actor> actors, uint color = default, bool allowDeadAndUntargetable = false)
    {
        foreach (var a in actors)
        {
            Actor(a, color == default ? Colors.Enemy : color, allowDeadAndUntargetable);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Actors(List<Actor> actors, uint color = default, bool allowDeadAndUntargetable = false)
    {
        var count = actors.Count;
        for (var i = 0; i < count; ++i)
        {
            Actor(actors[i], color == default ? Colors.Enemy : color, allowDeadAndUntargetable);
        }
    }

    public void Actors(BossModule module, uint[] actors, uint color = default, bool allowDeadAndUntargetable = false)
    {
        var actors_ = actors;
        var len = actors_.Length;
        var color_ = color == default ? Colors.Enemy : color;
        for (var i = 0; i < len; ++i)
        {
            var enemies = module.Enemies(actors[i]);
            var count = enemies.Count;
            for (var j = 0; j < count; ++j)
            {
                var enemy = enemies[j];
                if (!enemy.IsDestroyed && (allowDeadAndUntargetable || enemy.IsTargetable && !enemy.IsDead))
                {
                    Actor(enemy.Position, enemy.Rotation, color_);
                }
            }
        }
    }

    public void ActorsInBounds(BossModule module, uint[] actors, uint color = default, bool allowDeadAndUntargetable = false)
    {
        var actors_ = actors;
        var len = actors_.Length;
        var center = _center;
        var radius = Bounds.Radius;
        var color_ = color == default ? Colors.Enemy : color;
        for (var i = 0; i < len; ++i)
        {
            var enemies = module.Enemies(actors[i]);
            var count = enemies.Count;
            for (var j = 0; j < count; ++j)
            {
                var enemy = enemies[j];
                if (!enemy.IsDestroyed && enemy.Position.AlmostEqual(center, radius) && (allowDeadAndUntargetable || enemy.IsTargetable && !enemy.IsDead))
                {
                    Actor(enemy.Position, enemy.Rotation, color_);
                }
            }
        }
    }

    public static void End()
    {
        // Flush the final contiguous run while the arena clip rect is still active
        Dx11ArenaRenderer.EndArena();
        ImGui.GetWindowDrawList().PopClipRect();
    }
}
