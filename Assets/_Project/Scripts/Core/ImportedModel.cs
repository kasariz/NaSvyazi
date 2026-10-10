using System.Collections.Generic;
using UnityEngine;

namespace NaSvyazi.Core
{
    /// <summary>
    /// Метка «простые фигуры заменены 3D-моделью». Хранит выключенные рендереры исходной модели,
    /// чтобы команда «Вернуть простые модели» могла всё вернуть как было.
    /// Ставится редакторской командой «На связи → 3D-модели → Подключить модели».
    /// </summary>
    public class ImportedModel : MonoBehaviour
    {
        public const string ChildName = "ImportedModel";

        [Tooltip("Рендереры простой модели, выключенные при замене")]
        public List<Renderer> hiddenRenderers = new List<Renderer>();

        [Tooltip("Файл модели, из которого собрана замена")]
        public string sourcePath;
    }
}
