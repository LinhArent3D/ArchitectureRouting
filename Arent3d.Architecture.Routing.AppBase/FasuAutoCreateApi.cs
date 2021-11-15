﻿using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Arent3d.Architecture.Routing.AppBase
{
    public class FasuAutoCreateApi
    {
        public static IList<Element> GetAllSpaces(Document document)
        {
            ElementCategoryFilter filter = new ElementCategoryFilter(BuiltInCategory.OST_MEPSpaces);
            FilteredElementCollector collector = new FilteredElementCollector(document);
            IList<Element> spaces = collector.WherePasses(filter).WhereElementIsNotElementType().ToElements();
            return spaces;
        }

        public static IList<Element> GetAllGroups(Document document)
        {
            ElementCategoryFilter filter = new ElementCategoryFilter(BuiltInCategory.OST_IOSModelGroups);
            FilteredElementCollector collector = new FilteredElementCollector(document);
            IList<Element> groups = collector.WherePasses(filter).WhereElementIsNotElementType().ToElements();
            return groups;
        }

        public static bool GetHeightFasu(Document document, string nameFasu, ref double height)
        {
            bool brc = false;
            ElementCategoryFilter filter = new ElementCategoryFilter(BuiltInCategory.OST_DuctAccessory);
            FilteredElementCollector collector = new FilteredElementCollector(document);
            IList<Element> ducts = collector.WherePasses(filter).WhereElementIsNotElementType().ToElements();
            foreach (var duct in ducts)
            {
                if (duct.Name.IndexOf(nameFasu, 0) != -1)
                {
                    var locationPoint = (duct.Location as LocationPoint)!;
                    height = locationPoint.Point.Z;
                    brc = true;
                    break;
                }
            }

            return brc;
        }
    }
}