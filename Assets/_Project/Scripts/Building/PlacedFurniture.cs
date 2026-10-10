using System;
using NaSvyazi.Interaction;
using NaSvyazi.Player;
using UnityEngine;

namespace NaSvyazi.Building
{
    /// <summary>
    /// Установленная мебель (стеллаж). Хранит тип, уникальный номер экземпляра
    /// (для сохранения содержимого полок) и габариты для проверки места.
    /// E по каркасу — переставить.
    /// </summary>
    public class PlacedFurniture : MonoBehaviour, IInteractable
    {
        [Tooltip("Идентификатор вида мебели (FurnitureData.id)")]
        [SerializeField] private string furnitureId;

        [Tooltip("Уникальный номер экземпляра — по нему сохраняется содержимое полок")]
        [SerializeField] private string instanceId;

        [Tooltip("Габариты: ширина, высота, глубина (м)")]
        [SerializeField] private Vector3 footprint = new Vector3(1.8f, 1.9f, 0.45f);

        public string FurnitureId => furnitureId;
        public string InstanceId => instanceId;
        public Vector3 Footprint => footprint;

        public void SetInstanceId(string id) => instanceId = id;

        public void AssignNewInstanceId() => instanceId = Guid.NewGuid().ToString("N").Substring(0, 12);

        private void Awake()
        {
            if (string.IsNullOrEmpty(instanceId)) AssignNewInstanceId();
        }

        public string GetPrompt(PlayerInteractor interactor) => "Переставить";

        public bool CanInteract(PlayerInteractor interactor) =>
            FurniturePlacer.Instance != null && !FurniturePlacer.IsPlacing && !interactor.Carry.IsCarrying;

        public void Interact(PlayerInteractor interactor) => FurniturePlacer.Instance.BeginMove(this);
    }
}
