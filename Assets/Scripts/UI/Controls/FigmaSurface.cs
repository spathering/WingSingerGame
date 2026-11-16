using UnityEngine;
using UnityEngine.UI;

namespace WingSinger.UI {
    // Native, editable rounded surfaces; no flattened screen textures or per-frame allocations.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FigmaSurface : MaskableGraphic {
        public float radius, borderWidth;
        public Color borderColor = Color.clear;
        public Texture2D backdrop;
        public Rect backdropUV = new Rect(0, 0, 1, 1);
        public override Texture mainTexture => backdrop != null ? backdrop : s_WhiteTexture;
        protected override void OnPopulateMesh(VertexHelper mesh) {
            mesh.Clear(); var r = rectTransform.rect;
            float corner = Mathf.Clamp(radius, 0, Mathf.Min(r.width, r.height) / 2);
            const int segments = 12;
            Add(mesh, r.center, color, r);
            for (int side = 0; side < 4; side++) for (int i = 0; i <= segments; i++)
                Add(mesh, Corner(r, corner, side, i / (float)segments), color, r);
            int count = 4 * (segments + 1);
            for (int i = 0; i < count; i++) mesh.AddTriangle(0, i + 1, (i + 1) % count + 1);
            if (borderWidth <= 0 || borderColor.a <= 0) return;
            var inner = new Rect(r.x + borderWidth, r.y + borderWidth, Mathf.Max(0, r.width - 2 * borderWidth), Mathf.Max(0, r.height - 2 * borderWidth));
            int offset = mesh.currentVertCount;
            for (int side = 0; side < 4; side++) for (int i = 0; i <= segments; i++) {
                Add(mesh, Corner(r, corner, side, i / (float)segments), borderColor, r);
                Add(mesh, Corner(inner, Mathf.Max(0, corner - borderWidth), side, i / (float)segments), borderColor, r);
            }
            for (int i = 0; i < count; i++) {
                int a = offset + 2 * i, b = offset + 2 * ((i + 1) % count);
                mesh.AddTriangle(a, b, a + 1); mesh.AddTriangle(a + 1, b, b + 1);
            }
        }
        void Add(VertexHelper mesh, Vector2 p, Color c, Rect r) {
            Vector2 uv = new Vector2(backdropUV.x + (p.x-r.xMin)/Mathf.Max(1,r.width)*backdropUV.width,
                backdropUV.y + (p.y-r.yMin)/Mathf.Max(1,r.height)*backdropUV.height);
            mesh.AddVert(p, c, uv);
        }
        static Vector2 Corner(Rect r, float radius, int side, float t) {
            float angle = (side * 90 + t * 90) * Mathf.Deg2Rad;
            Vector2 center = new Vector2(side == 0 || side == 3 ? r.xMax-radius : r.xMin+radius,
                side < 2 ? r.yMax-radius : r.yMin+radius);
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
    }
}
