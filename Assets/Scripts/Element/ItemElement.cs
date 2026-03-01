using System.Globalization;
using System.Xml;
using UnityEngine;
using Vectorier.Component;
using Vectorier.XML;

namespace Vectorier.Element
{
    public static class ItemElement
    {
        public static XmlElement WriteToXML(GameObject gameObject, XmlUtility xmlUtility, XmlElement parentElement)
        {
            if (gameObject == null || xmlUtility == null || parentElement == null)
                return null;

            if (!gameObject.TryGetComponent<ItemComponent>(out var itemComponent))
                return null;

            // <Item>
            XmlElement itemElement = xmlUtility.AddElement(parentElement, "Item");

            // Try to find <Properties> if it already exists; otherwise create it
            XmlElement propertiesElement = xmlUtility.GetOrCreateElement(itemElement, "Properties");
            XmlElement staticElement = xmlUtility.GetOrCreateElement(propertiesElement, "Static");

            // Compute coordinates
            Element.WritePosition(xmlUtility, itemElement, gameObject);

            // Write base attributes
            xmlUtility.SetAttribute(itemElement, "Type", ((int)itemComponent.Type).ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(itemElement, "Radius", itemComponent.Radius.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(itemElement, "Score", itemComponent.Score.ToString(CultureInfo.InvariantCulture));

            // Coins
            if (itemComponent.Type == ItemComponent.ItemType.Coin)
            {
                xmlUtility.SetAttribute(itemElement, "GroupId", itemComponent.GroupId.ToString(CultureInfo.InvariantCulture));
            }

            // Selection
            Element.WriteSelectionComponent(xmlUtility, staticElement, gameObject);

            return itemElement;
        }

        public static GameObject WriteToScene(XmlElement element, Transform parent, string factor)
        {
            if (element == null)
                return null;

            // Properties
            XmlElement propertiesElement = element.SelectSingleNode("Properties") as XmlElement;
            XmlElement staticElement = propertiesElement?.SelectSingleNode("Static") as XmlElement;

            // Create object
            GameObject itemObject = Element.CreateObject("Item", parent, element);

            // Apply
            Element.ApplyPosition(itemObject, element);
            Element.ApplyLayer(itemObject, factor);

            // Component
            ItemComponent itemComponent = itemObject.AddComponent<ItemComponent>();

            if (int.TryParse(element.GetAttribute("Type"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int typeValue))
            {
                if (System.Enum.IsDefined(typeof(ItemComponent.ItemType), typeValue))
                    itemComponent.Type = (ItemComponent.ItemType)typeValue;
            }

            if (float.TryParse(element.GetAttribute("Radius"), NumberStyles.Float, CultureInfo.InvariantCulture, out float radius))
                itemComponent.Radius = radius;

            if (int.TryParse(element.GetAttribute("Score"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int score))
                itemComponent.Score = score;

            if (element.HasAttribute("GroupId") && int.TryParse(element.GetAttribute("GroupId"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int gid))
                itemComponent.GroupId = gid;

            Element.ApplySelectionComponent(staticElement, itemObject);
            Element.ApplyDynamic(propertiesElement, itemObject);

            // Sprite
            SpriteRenderer renderer = itemObject.AddComponent<SpriteRenderer>();
            switch (itemComponent.Type)
            {
                case ItemComponent.ItemType.Bonus:
                    renderer.sprite = Resources.Load<Sprite>("Images/Editor/Item/pill");
                    itemObject.name = "Bonus";
                    break;

                case ItemComponent.ItemType.Coin:
                    renderer.sprite = Resources.Load<Sprite>("Images/Editor/Item/coin");
                    itemObject.name = "Coin";
                    break;
            }

            // Tag
            itemObject.tag = "Item";
            renderer.sortingOrder = 4;
            renderer.sortingLayerName = "OnTop";

            return itemObject;
        }

        public static GameObject Create(ItemComponent.ItemType type, Transform parent = null)
        {
            GameObject itemObject = new GameObject("Item");

            // Parent
            if (parent != null)
            {
                itemObject.transform.SetParent(parent, false);
                itemObject.transform.localPosition = Vector3.zero;
            }

            // Component
            ItemComponent itemComponent = itemObject.AddComponent<ItemComponent>();
            itemComponent.Type = type;

            // Sprite
            SpriteRenderer renderer = itemObject.AddComponent<SpriteRenderer>();
            switch (type)
            {
                case ItemComponent.ItemType.Bonus:
                    renderer.sprite = Resources.Load<Sprite>("Images/Editor/Item/pill");
                    itemObject.name = "Bonus";
                    break;

                case ItemComponent.ItemType.Coin:
                    renderer.sprite = Resources.Load<Sprite>("Images/Editor/Item/coin");
                    itemObject.name = "Coin";
                    break;
            }

            // Tag + sorting
            itemObject.tag = "Item";
            renderer.sortingOrder = 4;
            renderer.sortingLayerName = "OnTop";

            return itemObject;
        }
    }
}
