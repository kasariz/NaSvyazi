using UnityEngine;

namespace NaSvyazi.Core
{
    /// <summary>
    /// Материал для фигур, которые создаются во время игры (руки героя, товары из простых форм,
    /// полоса на коробке, грузовик, сотрудники, пятна).
    ///
    /// Почему это нужно: GameObject.CreatePrimitive в редакторе получает материал URP, а в собранной
    /// игре — стандартный материал встроенного конвейера, шейдера которого в сборке URP нет.
    /// Из-за этого в релизе такие фигуры были ярко-розовыми («нет шейдера»).
    /// Материал лежит в Resources, поэтому гарантированно попадает в сборку.
    /// </summary>
    public static class RuntimeMaterials
    {
        private static Material lit;

        public static Material Lit
        {
            get
            {
                if (lit == null) lit = Resources.Load<Material>("Materials/RuntimeLit");
                return lit;
            }
        }

        /// <summary>Создать примитив с материалом URP (вместо GameObject.CreatePrimitive).</summary>
        public static GameObject CreatePrimitive(PrimitiveType type)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && Lit != null) r.sharedMaterial = Lit;
            return go;
        }
    }
}
