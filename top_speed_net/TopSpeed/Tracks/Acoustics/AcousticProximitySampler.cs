using System;
using System.Collections.Generic;
using System.Numerics;
using TopSpeed.Tracks.Areas;
using TopSpeed.Tracks.Map;
using TopSpeed.Tracks.Materials;
using TopSpeed.Tracks.Topology;
using TopSpeed.Tracks.Walls;
using TS.Audio;

namespace TopSpeed.Tracks.Acoustics
{
    internal sealed class AcousticProximitySampler
    {
        private readonly Dictionary<string, ShapeDefinition> _shapes;
        private readonly List<TrackWallDefinition> _walls;
        private readonly TrackAreaManager _areaManager;
        private readonly Dictionary<string, ProximityMaterial> _materials;
        private readonly ProximityMaterial _defaultMaterial;
        private readonly float? _defaultCeilingHeight;

        private struct SideHit
        {
            public float Distance;
            public ProximityMaterial Material;
        }

        public AcousticProximitySampler(TrackMap map)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));

            _shapes = new Dictionary<string, ShapeDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var shape in map.Shapes)
            {
                if (shape == null)
                    continue;
                _shapes[shape.Id] = shape;
            }

            _walls = new List<TrackWallDefinition>();
            foreach (var wall in map.Walls)
            {
                if (wall == null)
                    continue;
                _walls.Add(wall);
            }

            _areaManager = map.BuildAreaManager();
            _defaultCeilingHeight = map.DefaultCeilingHeightMeters;

            _materials = new Dictionary<string, ProximityMaterial>(StringComparer.OrdinalIgnoreCase);
            foreach (var material in map.Materials)
            {
                if (material == null)
                    continue;
                _materials[material.Id] = ToProximityMaterial(material);
            }

            if (!string.IsNullOrWhiteSpace(map.DefaultMaterialId) &&
                _materials.TryGetValue(map.DefaultMaterialId.Trim(), out var defaultMat))
            {
                _defaultMaterial = defaultMat;
            }
            else
            {
                _defaultMaterial = ProximityMaterial.Neutral;
            }
        }

        public bool TryCompute(Vector3 worldPosition, Vector3 forward, out ProximityAcoustics proximity)
        {
            proximity = ProximityAcoustics.None;
            if (_walls.Count == 0)
                return false;

            var position = new Vector2(worldPosition.X, worldPosition.Z);
            var forward2 = new Vector2(forward.X, forward.Z);
            if (forward2.LengthSquared() < 0.0001f)
                forward2 = new Vector2(0f, 1f);
            else
                forward2 = Vector2.Normalize(forward2);

            var left = new SideHit { Distance = float.PositiveInfinity, Material = _defaultMaterial };
            var right = new SideHit { Distance = float.PositiveInfinity, Material = _defaultMaterial };
            var front = new SideHit { Distance = float.PositiveInfinity, Material = _defaultMaterial };
            var back = new SideHit { Distance = float.PositiveInfinity, Material = _defaultMaterial };

            for (int i = 0; i < _walls.Count; i++)
            {
                var wall = _walls[i];
                if (wall == null)
                    continue;
                if (wall.CollisionMode == TrackWallCollisionMode.Pass)
                    continue;
                if (!_shapes.TryGetValue(wall.ShapeId, out var shape))
                    continue;

                if (!TryGetClosestPoint(shape, wall, position, out var closest, out var distance))
                    continue;

                var toWall = closest - position;
                var lenSq = toWall.LengthSquared();
                if (lenSq <= 0.000001f)
                    continue;
                var dir = toWall / (float)Math.Sqrt(lenSq);
                var material = ResolveMaterial(wall.MaterialId);

                var cross = forward2.X * dir.Y - forward2.Y * dir.X;
                if (cross >= 0f)
                    UpdateSide(ref left, distance, material);
                else
                    UpdateSide(ref right, distance, material);

                var dot = Vector2.Dot(forward2, dir);
                if (dot >= 0f)
                    UpdateSide(ref front, distance, material);
                else
                    UpdateSide(ref back, distance, material);
            }

            var hasAny = !(float.IsPositiveInfinity(left.Distance) &&
                           float.IsPositiveInfinity(right.Distance) &&
                           float.IsPositiveInfinity(front.Distance) &&
                           float.IsPositiveInfinity(back.Distance));

            var hasCeiling = TryGetCeiling(worldPosition, position, out var ceilingDistance, out var ceilingMaterial);

            if (!hasAny && !hasCeiling)
                return false;

            proximity = new ProximityAcoustics
            {
                HasProximity = true,
                LeftMeters = NormalizeDistance(left.Distance),
                RightMeters = NormalizeDistance(right.Distance),
                FrontMeters = NormalizeDistance(front.Distance),
                BackMeters = NormalizeDistance(back.Distance),
                CeilingMeters = hasCeiling ? NormalizeDistance(ceilingDistance) : -1f,
                LeftMaterial = left.Material,
                RightMaterial = right.Material,
                FrontMaterial = front.Material,
                BackMaterial = back.Material,
                CeilingMaterial = hasCeiling ? ceilingMaterial : _defaultMaterial
            };
            return true;
        }

        private static float NormalizeDistance(float value)
        {
            if (float.IsPositiveInfinity(value) || float.IsNaN(value))
                return -1f;
            return value < 0f ? 0f : value;
        }

        private static void UpdateSide(ref SideHit side, float distance, ProximityMaterial material)
        {
            if (distance < side.Distance)
            {
                side.Distance = distance;
                side.Material = material;
            }
        }

        private bool TryGetCeiling(
            Vector3 worldPosition,
            Vector2 position,
            out float distance,
            out ProximityMaterial material)
        {
            distance = -1f;
            material = _defaultMaterial;

            var areas = _areaManager.FindAreasContaining(position);
            TrackAreaDefinition? materialArea = null;
            float? ceilingHeight = null;
            if (areas.Count > 0)
            {
                foreach (var area in areas)
                {
                    if (area == null)
                        continue;
                    materialArea = area;
                    if (area.CeilingHeightMeters.HasValue)
                        ceilingHeight = area.CeilingHeightMeters.Value;
                }
            }

            if (!ceilingHeight.HasValue)
                ceilingHeight = _defaultCeilingHeight;

            if (!ceilingHeight.HasValue)
                return false;

            if (materialArea != null && !string.IsNullOrWhiteSpace(materialArea.MaterialId))
                material = ResolveMaterial(materialArea.MaterialId);

            distance = ceilingHeight.Value - worldPosition.Y;
            if (distance < 0f)
                distance = 0f;
            return true;
        }

        private ProximityMaterial ResolveMaterial(string? materialId)
        {
            var key = materialId?.Trim();
            if (!string.IsNullOrWhiteSpace(key) && _materials.TryGetValue(key!, out var material))
                return material;

            return _defaultMaterial;
        }

        private static ProximityMaterial ToProximityMaterial(TrackMaterialDefinition material)
        {
            return new ProximityMaterial
            {
                AbsorptionLow = material.AbsorptionLow,
                AbsorptionMid = material.AbsorptionMid,
                AbsorptionHigh = material.AbsorptionHigh,
                Scattering = material.Scattering,
                TransmissionLow = material.TransmissionLow,
                TransmissionMid = material.TransmissionMid,
                TransmissionHigh = material.TransmissionHigh
            };
        }

        private static bool TryGetClosestPoint(
            ShapeDefinition shape,
            TrackWallDefinition wall,
            Vector2 position,
            out Vector2 closest,
            out float distance)
        {
            closest = position;
            distance = float.PositiveInfinity;
            if (shape == null || wall == null)
                return false;

            switch (shape.Type)
            {
                case ShapeType.Rectangle:
                    closest = ClosestPointOnRectanglePerimeter(position, shape);
                    break;
                case ShapeType.Circle:
                    closest = ClosestPointOnCircle(position, shape);
                    break;
                case ShapeType.Ring:
                    closest = ClosestPointOnRing(position, shape);
                    break;
                case ShapeType.Polygon:
                    closest = ClosestPointOnPolygon(position, shape.Points);
                    break;
                case ShapeType.Polyline:
                    closest = ClosestPointOnPolyline(position, shape.Points);
                    break;
                default:
                    return false;
            }

            distance = Vector2.Distance(position, closest);
            var halfWidth = Math.Max(0f, wall.WidthMeters) * 0.5f;
            if (halfWidth > 0f)
                distance = Math.Max(0f, distance - halfWidth);
            return true;
        }

        private static Vector2 ClosestPointOnRectanglePerimeter(Vector2 position, ShapeDefinition shape)
        {
            var minX = Math.Min(shape.X, shape.X + shape.Width);
            var maxX = Math.Max(shape.X, shape.X + shape.Width);
            var minZ = Math.Min(shape.Z, shape.Z + shape.Height);
            var maxZ = Math.Max(shape.Z, shape.Z + shape.Height);

            var clampedX = Clamp(position.X, minX, maxX);
            var clampedZ = Clamp(position.Y, minZ, maxZ);

            var inside = position.X >= minX && position.X <= maxX &&
                         position.Y >= minZ && position.Y <= maxZ;
            if (!inside)
                return new Vector2(clampedX, clampedZ);

            var distLeft = position.X - minX;
            var distRight = maxX - position.X;
            var distTop = maxZ - position.Y;
            var distBottom = position.Y - minZ;

            var min = distLeft;
            var edge = 0;
            if (distRight < min) { min = distRight; edge = 1; }
            if (distTop < min) { min = distTop; edge = 2; }
            if (distBottom < min) { min = distBottom; edge = 3; }

            switch (edge)
            {
                case 0:
                    return new Vector2(minX, position.Y);
                case 1:
                    return new Vector2(maxX, position.Y);
                case 2:
                    return new Vector2(position.X, maxZ);
                default:
                    return new Vector2(position.X, minZ);
            }
        }

        private static Vector2 ClosestPointOnCircle(Vector2 position, ShapeDefinition shape)
        {
            var radius = Math.Abs(shape.Radius);
            var center = new Vector2(shape.X, shape.Z);
            var toPoint = position - center;
            var length = toPoint.Length();
            if (length <= float.Epsilon)
                return center + new Vector2(radius, 0f);
            if (radius <= 0f)
                return center;
            return center + (toPoint / length) * radius;
        }

        private static Vector2 ClosestPointOnRing(Vector2 position, ShapeDefinition shape)
        {
            var radius = Math.Abs(shape.Radius);
            var ringWidth = Math.Abs(shape.RingWidth);
            if (radius > 0f)
            {
                var inner = radius;
                var outer = radius + ringWidth;
                var center = new Vector2(shape.X, shape.Z);
                var toPoint = position - center;
                var length = toPoint.Length();
                if (length <= float.Epsilon)
                    return center + new Vector2(outer > 0f ? outer : inner, 0f);
                var target = outer > 0f && Math.Abs(length - outer) < Math.Abs(length - inner) ? outer : inner;
                return center + (toPoint / length) * target;
            }

            return ClosestPointOnRectanglePerimeter(position, shape);
        }

        private static Vector2 ClosestPointOnPolyline(Vector2 position, IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count == 0)
                return position;
            if (points.Count == 1)
                return points[0];

            var best = points[0];
            var bestDist = float.MaxValue;
            for (var i = 0; i < points.Count - 1; i++)
            {
                var candidate = ClosestPointOnSegment(points[i], points[i + 1], position);
                var dist = Vector2.DistanceSquared(candidate, position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = candidate;
                }
            }
            return best;
        }

        private static Vector2 ClosestPointOnPolygon(Vector2 position, IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count == 0)
                return position;

            var best = points[0];
            var bestDist = float.MaxValue;
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                var candidate = ClosestPointOnSegment(a, b, position);
                var dist = Vector2.DistanceSquared(candidate, position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = candidate;
                }
            }
            return best;
        }

        private static Vector2 ClosestPointOnSegment(Vector2 a, Vector2 b, Vector2 p)
        {
            var ab = b - a;
            var ap = p - a;
            var abLenSq = Vector2.Dot(ab, ab);
            if (abLenSq <= float.Epsilon)
                return a;

            var t = Vector2.Dot(ap, ab) / abLenSq;
            if (t < 0f)
                t = 0f;
            else if (t > 1f)
                t = 1f;

            return a + (ab * t);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
