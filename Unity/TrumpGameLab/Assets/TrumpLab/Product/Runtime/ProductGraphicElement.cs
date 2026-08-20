#nullable enable

using System;
using UnityEngine;
using UnityEngine.UI;

namespace TrumpLab.Product
{
    [DisallowMultipleComponent]
    public sealed class ProductGraphicElement : MonoBehaviour
    {
        [SerializeField] private Graphic? targetGraphic;
        [SerializeField] private ProductGraphicRole baseRole;
        [SerializeField] private bool preserveAlpha;
        [SerializeField] private bool hasBaseRole;

        public Graphic TargetGraphic => targetGraphic ?? throw Missing(nameof(targetGraphic));
        public ProductGraphicRole BaseRole => hasBaseRole
            ? baseRole
            : throw Missing(nameof(baseRole));
        public bool PreserveAlpha => hasBaseRole
            ? preserveAlpha
            : throw Missing(nameof(preserveAlpha));

        public void Configure(Graphic graphic, ProductGraphicRole role,
            bool preserveAlpha = false)
        {
            if (graphic == null) throw new ArgumentNullException(nameof(graphic));
            RequireRole(role);
            if (hasBaseRole && (targetGraphic != graphic || baseRole != role ||
                this.preserveAlpha != preserveAlpha))
                throw new InvalidOperationException(
                    "A product graphic element's base role and alpha policy are immutable.");
            targetGraphic = graphic;
            baseRole = role;
            this.preserveAlpha = preserveAlpha;
            hasBaseRole = true;
        }

        public void Apply(ProductUiPalette palette)
        {
            if (palette == null) throw new ArgumentNullException(nameof(palette));
            Color color = palette.ColorFor(BaseRole);
            if (PreserveAlpha) color.a = TargetGraphic.color.a;
            TargetGraphic.color = color;
        }

        private static void RequireRole(ProductGraphicRole role)
        {
            if (!Enum.IsDefined(typeof(ProductGraphicRole), role))
                throw new ArgumentOutOfRangeException(nameof(role), role,
                    "Unknown product graphic role.");
        }

        private static InvalidOperationException Missing(string name) =>
            new InvalidOperationException(
                "Product graphic element is not configured: " + name);
    }
}
