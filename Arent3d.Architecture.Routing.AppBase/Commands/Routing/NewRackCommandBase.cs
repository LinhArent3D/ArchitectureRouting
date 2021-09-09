﻿using System;
using Arent3d.Revit;
using Arent3d.Revit.I18n;
using Arent3d.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Electrical;

namespace Arent3d.Architecture.Routing.AppBase.Commands.Routing
{
    public abstract class NewRackCommandBase : IExternalCommand
    {
        private static readonly double DefaultThickness = 200.0;
        private static readonly double DefaultWidth = 100.0;
        private static readonly double DefaultHeight = 4000.0 ;
    protected abstract AddInType GetAddInType() ;

    public Result Execute( ExternalCommandData commandData, ref string message, ElementSet elements )
    {
      var uiDocument = commandData.Application.ActiveUIDocument ;
      var document = uiDocument.Document ;
      try {
                // 線クリックのui設定＿Setting UI of wire click
        var pickFrom = PointOnRoutePicker.PickRoute(uiDocument, false, "Pick a point on a route.", GetAddInType());
        var pickTo = PointOnRoutePicker.PickRoute(uiDocument, false, "Pick a point on a route.", GetAddInType());

        // TODO 電動二方弁でコネクタ設定時エラーが出る（おそらくコネクタタイプの問題）＿Error occurs when setting the connector with an motor two-way valve (probably connector type problem)

        if ( null == pickFrom.Position || null == pickTo.Position || null == pickFrom.RouteDirection ||
             null == pickTo.RouteDirection) {
          return Result.Failed ;
        }

        var result = document.Transaction("TransactionName.Commands.Rack.Import".GetAppStringByKeyOrDefault( "Import Rack" ), _ =>
          {
              var routeName = RoutingElementExtensions.GetRouteName(pickFrom.Element);
              if (routeName != null)
              {
                  // get all elements in route
                  var allElementsInRoute = document.GetAllElementsOfRouteName<Element>(routeName);

                  // Browse each conduits and draw the cable tray below
                  foreach (var element in allElementsInRoute)
                  {
                      if (element is Conduit) // element is straight conduit
                      {
                          var conduit = (element as Conduit)!;
                          var location = (element.Location as LocationCurve)!;
                          var line = (location.Curve as Line)!;

                          var length = conduit.ParametersMap.get_Item("Length").AsDouble();
                          var bounding = conduit.get_BoundingBox(uiDocument.ActiveView)!;
                          var endPos = line.Origin.Multiply(length);

                          var symbol = uiDocument.Document.GetFamilySymbol(RoutingFamilyType.RackGuide)!; // TODO using Cable Tray family

                          // Create cable tray
                          var instance = symbol.Instantiate(
                                            new XYZ(line.Origin.X, line.Origin.Y, line.Origin.Z),
                                            uiDocument.ActiveView.GenLevel, StructuralType.NonStructural);

                          // settings cable tray length, thickness, direction, postion
                          if (line.Direction == XYZ.BasisX)
                          {
                              instance.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM).Set(0.0);
                              SetParameter(instance, "幅", DefaultWidth.MillimetersToRevitUnits()); // TODO change parameter
                              SetParameter(instance, "高さ", DefaultThickness.MillimetersToRevitUnits()); // TODO change parameter
                              SetParameter(instance, "奥行き", length); // TODO change parameter
                              ElementTransformUtils.RotateElement(document, instance.Id, Line.CreateBound(XYZ.Zero, XYZ.BasisX), (Math.PI / 180) * (90));
                              instance.Location.Move(new XYZ(length, 0, 0));
                              SetParameter(instance, "Arent-Offset", 0);
                          }
                          else
                          {
                              instance.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM).Set(0.0);
                              SetParameter(instance, "幅", DefaultWidth.MillimetersToRevitUnits()); // TODO change parameter
                              SetParameter(instance, "高さ", DefaultThickness.MillimetersToRevitUnits()); // TODO change parameter
                              SetParameter(instance, "奥行き", length); // TODO change parameter
                              instance.Location.Move(new XYZ(0, length, 0));
                              SetParameter(instance, "Arent-Offset", 0);
                          }

                      } else // element is conduit fitting
                      {
                          var conduit = (element as FamilyInstance)!;

                          var location = (element.Location as LocationPoint)!;
                          var line = Line.CreateBound(new XYZ(0, 0, 0), new XYZ(0, 1, 0));

                          var length = conduit.ParametersMap.get_Item("呼び半径").AsDouble();
                          var bounding = conduit.get_BoundingBox(uiDocument.ActiveView)!;
                          var endPos = line!.Origin.Multiply(length);

                          var symbol = uiDocument.Document.GetFamilySymbol(RoutingFamilyType.RackGuide)!; // TODO using Cable Tray fitting family

                          var instance = symbol.Instantiate(
                                            new XYZ(location!.Point.X, location!.Point.Y, DefaultHeight.MillimetersToRevitUnits()),
                                            uiDocument.ActiveView.GenLevel, StructuralType.NonStructural);

                          // settings cable tray length, thickness, direction, postion
                          if (line.Direction.X == 1.0)
                          {
                              instance.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM).Set(0.0); // TODO change parameter
                              SetParameter(instance, "幅", DefaultWidth.MillimetersToRevitUnits()); // TODO change parameter
                              SetParameter(instance, "高さ", DefaultThickness.MillimetersToRevitUnits()); // TODO change parameter
                              SetParameter(instance, "奥行き", length); // TODO change parameter
                              ElementTransformUtils.RotateElement(document, instance.Id, line, (Math.PI / 180) * (90));
                              SetParameter(instance, "Arent-Offset", 0);
                          }
                          else
                          {
                              instance.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM).Set(0.0);
                              SetParameter(instance, "幅", DefaultWidth.MillimetersToRevitUnits()); // TODO change parameter
                              SetParameter(instance, "高さ", DefaultThickness.MillimetersToRevitUnits()); // TODO change parameter
                              SetParameter(instance, "奥行き", length); // TODO change parameter
                              SetParameter(instance, "Arent-Offset", 0);
                          }
                      }
                  }
              }

            return Result.Succeeded ;
          } ) ;

        return result ;
      }
      catch ( Autodesk.Revit.Exceptions.OperationCanceledException ) {
        return Result.Cancelled ;
      }
      catch ( Exception e ) {
        CommandUtils.DebugAlertException( e ) ;
        return Result.Failed ;
      }
    }

    private static void SetParameter( FamilyInstance instance, string parameterName, double value )
    {
      instance.ParametersMap.get_Item( parameterName )?.Set( value ) ;
    }
  }
}