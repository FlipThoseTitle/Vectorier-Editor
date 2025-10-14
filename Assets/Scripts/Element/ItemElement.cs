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

            ItemComponent itemComponent = gameObject.GetComponent<ItemComponent>();
            if (itemComponent == null)
                return null;

            // Create <Item> node
            XmlElement itemElement = xmlUtility.AddElement(parentElement, "Item");

            // Compute coordinates (same convention as others)
            Vector3 pos = gameObject.transform.position;
            float x = pos.x * 100f;
            float y = pos.y * -100f;

            // Write base attributes
            xmlUtility.SetAttribute(itemElement, "X", x.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(itemElement, "Y", y.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(itemElement, "Type", ((int)itemComponent.Type).ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(itemElement, "Radius", itemComponent.Radius.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(itemElement, "Score", itemComponent.Score.ToString(CultureInfo.InvariantCulture));

            // Coins
            if (itemComponent.Type == ItemComponent.ItemType.Coin)
            {
                xmlUtility.SetAttribute(itemElement, "GroupId", itemComponent.GroupId.ToString(CultureInfo.InvariantCulture));
            }

            return itemElement;
        }
    }
}
