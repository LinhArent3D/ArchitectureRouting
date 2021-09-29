﻿using System ;
using System.Linq ;
using Arent3d.Revit ;
using Arent3d.Revit.I18n ;
using Arent3d.Revit.UI ;
using Autodesk.Revit.DB ;
using Autodesk.Revit.DB.Structure ;
using Autodesk.Revit.UI ;
using Autodesk.Revit.DB.Electrical ;
using System.Collections.Generic ;

namespace Arent3d.Architecture.Routing.AppBase.Commands.Routing
{
  public abstract class NewRackCommandBase : IExternalCommand
  {
    /// <summary>
    /// Max Distance Tolerance when find Connector Closest
    /// </summary>
    private static readonly double maxDistanceTolerance = ( 10.0 ).MillimetersToRevitUnits() ;

    private readonly BuiltInCategory[] ConduitBuiltInCategories =
    {
      BuiltInCategory.OST_Conduit, BuiltInCategory.OST_ConduitFitting, BuiltInCategory.OST_ConduitRun
    } ;

    private static readonly BuiltInCategory[] CableTrayBuiltInCategories =
    {
      BuiltInCategory.OST_CableTray, BuiltInCategory.OST_CableTrayFitting
    } ;

    protected abstract AddInType GetAddInType() ;

    public Result Execute( ExternalCommandData commandData, ref string message, ElementSet elements )
    {
      var uiDocument = commandData.Application.ActiveUIDocument ;
      var document = uiDocument.Document ;
      try {
        var result = document.Transaction(
          "TransactionName.Commands.Rack.CreateCableRackFroAllRoute".GetAppStringByKeyOrDefault(
            "Create Cable Rack For All Route" ), _ =>
          {
            var parameterName = document.GetParameterName( RoutingParameter.RouteName ) ;
            if ( null == parameterName ) return Result.Failed ;

            var filter =
              new ElementParameterFilter(
                ParameterFilterRuleFactory.CreateSharedParameterApplicableRule( parameterName ) ) ;

            // get all route names
            var routeNames = document.GetAllElements<Element>().OfCategory( ConduitBuiltInCategories )
              .OfNotElementType().Where( filter ).OfType<Element>()
              .Select( x => RoutingElementExtensions.GetRouteName( x ) ).Distinct() ;

            // create cable rack for each route
            foreach ( var routeName in routeNames ) {
              CreateCableRackForRoute( uiDocument, routeName ) ;
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

    public static void SetParameter( FamilyInstance instance, string parameterName, double value )
    {
      instance.ParametersMap.get_Item( parameterName )?.Set( value ) ;
    }

    /// <summary>
    /// Creat cable rack for route
    /// </summary>
    /// <param name="uiDocument"></param>
    /// <param name="routeName"></param>
    private void CreateCableRackForRoute( UIDocument uiDocument, string? routeName )
    {
      if ( routeName != null ) {
        var document = uiDocument.Document ;
        // get all elements in route
        var allElementsInRoute = document.GetAllElementsOfRouteName<Element>( routeName ) ;

        var connectors = new List<Connector>() ;
        // Browse each conduits and draw the cable tray below
        foreach ( var element in allElementsInRoute ) {
          using var transaction = new SubTransaction( document ) ;
          try {
            transaction.Start() ;
            if ( element is Conduit ) // element is straight conduit
            {
              var conduit = ( element as Conduit )! ;

              var location = ( element.Location as LocationCurve )! ;
              var line = ( location.Curve as Line )! ;

              // Ignore the case of vertical conduits in the oz direction
              if ( 1.0 == line.Direction.Z || -1.0 == line.Direction.Z ) {
                continue ;
              }

              Connector firstConnector = GetFirstConnector( element.GetConnectorManager()!.Connectors )! ;

              var length = conduit.ParametersMap
                .get_Item(
                  "Revit.Property.Builtin.Conduit.Length".GetDocumentStringByKeyOrDefault( document, "Length" ) )
                .AsDouble() ;
              var diameter = conduit.ParametersMap
                .get_Item( "Revit.Property.Builtin.OutsideDiameter".GetDocumentStringByKeyOrDefault( document,
                  "Outside Diameter" ) ).AsDouble() ;

              var symbol =
                uiDocument.Document.GetFamilySymbol( RoutingFamilyType.CableTray )! ; // TODO may change in the future

              // Create cable tray
              var instance = symbol.Instantiate(
                new XYZ( firstConnector.Origin.X, firstConnector.Origin.Y, firstConnector.Origin.Z ),
                uiDocument.ActiveView.GenLevel, StructuralType.NonStructural ) ;

              // set cable rack length
              SetParameter( instance,
                "Revit.Property.Builtin.TrayLength".GetDocumentStringByKeyOrDefault( document, "トレイ長さ" ),
                length ) ; // TODO may be must change when FamilyType change

              // move cable rack to under conduit
              instance.Location.Move( new XYZ( 0, 0, -diameter ) ) ; // TODO may be must change when FamilyType change

              // set cable tray direction
              if ( 1.0 == line.Direction.Y ) {
                ElementTransformUtils.RotateElement( document, instance.Id,
                  Line.CreateBound(
                    new XYZ( firstConnector.Origin.X, firstConnector.Origin.Y, firstConnector.Origin.Z ),
                    new XYZ( firstConnector.Origin.X, firstConnector.Origin.Y, firstConnector.Origin.Z + 1 ) ),
                  Math.PI / 2 ) ;
              } else if ( -1.0 == line.Direction.Y ) {
                ElementTransformUtils.RotateElement( document, instance.Id,
                  Line.CreateBound(
                    new XYZ( firstConnector.Origin.X, firstConnector.Origin.Y, firstConnector.Origin.Z ),
                    new XYZ( firstConnector.Origin.X, firstConnector.Origin.Y, firstConnector.Origin.Z - 1 ) ),
                  Math.PI / 2 ) ;
              } else if ( -1.0 == line.Direction.X ) {
                ElementTransformUtils.RotateElement( document, instance.Id,
                  Line.CreateBound(
                    new XYZ( firstConnector.Origin.X, firstConnector.Origin.Y, firstConnector.Origin.Z ),
                    new XYZ( firstConnector.Origin.X, firstConnector.Origin.Y, firstConnector.Origin.Z - 1 ) ),
                  Math.PI ) ;
              }

              // check cable tray exists
              if ( ExistsCableTray( document, instance ) ) {
                transaction.RollBack() ;
                continue ;
              }

              // save connectors of cable rack
              foreach ( Connector connector in instance.GetConnectorManager()!.Connectors ) {
                connectors.Add( connector ) ;
              }
            }
            else // element is conduit fitting
            {
              var conduit = ( element as FamilyInstance )! ;

              // Ignore the case of vertical conduits in the oz direction
              if ( 1.0 == conduit.FacingOrientation.Z || -1.0 == conduit.FacingOrientation.Z) {
                continue ;
              }

              var location = ( element.Location as LocationPoint )! ;

              var length = conduit.ParametersMap
                .get_Item("Revit.Property.Builtin.ConduitFitting.Length".GetDocumentStringByKeyOrDefault( document, "電線管長さ") )
                .AsDouble() ;
              var diameter = conduit.ParametersMap
                .get_Item( "Revit.Property.Builtin.NominalDiameter".GetDocumentStringByKeyOrDefault( document, "呼び径" ) )
                .AsDouble() ;
              var bendRadius = conduit.ParametersMap
                .get_Item( "Revit.Property.Builtin.BendRadius".GetDocumentStringByKeyOrDefault( document,
                  "Bend Radius" ) ).AsDouble() ;

              var symbol =
                uiDocument.Document.GetFamilySymbol( RoutingFamilyType
                  .CableTrayFitting )! ; // TODO may change in the future

              var instance = symbol.Instantiate( new XYZ( location.Point.X, location.Point.Y, location.Point.Z ),
                uiDocument.ActiveView.GenLevel, StructuralType.NonStructural ) ;

              // set cable tray Bend Radius
              SetParameter( instance,
                "Revit.Property.Builtin.BendRadius".GetDocumentStringByKeyOrDefault( document, "Bend Radius" ),
                bendRadius / 2 ) ; // TODO may be must change when FamilyType change
                            
              // set cable rack length
              SetParameter( instance,
                "Revit.Property.Builtin.TrayLength".GetDocumentStringByKeyOrDefault( document, "トレイ長さ" ),
                length ) ; // TODO may be must change when FamilyType change

              // set cable tray fitting direction
              if ( 1.0 == conduit.FacingOrientation.X ) {
                instance.Location.Rotate(
                  Line.CreateBound( new XYZ( location.Point.X, location.Point.Y, location.Point.Z ),
                    new XYZ( location.Point.X, location.Point.Y, location.Point.Z - 1 ) ), Math.PI / 2 ) ;
              }
              else if ( -1.0 == conduit.FacingOrientation.X ) {
                instance.Location.Rotate(
                  Line.CreateBound( new XYZ( location.Point.X, location.Point.Y, location.Point.Z ),
                    new XYZ( location.Point.X, location.Point.Y, location.Point.Z + 1 ) ), Math.PI / 2 ) ;
              }
              else if ( -1.0 == conduit.FacingOrientation.Y ) {
                instance.Location.Rotate(
                  Line.CreateBound( new XYZ( location.Point.X, location.Point.Y, location.Point.Z ),
                    new XYZ( location.Point.X, location.Point.Y, location.Point.Z + 1 ) ), Math.PI ) ;
              }

              // move cable rack to under conduit
              instance.Location.Move( new XYZ( 0, 0, -diameter ) ) ; // TODO may be must change when FamilyType change

              // check cable tray exists
              if ( ExistsCableTray(document, instance) ) {
                transaction.RollBack() ;
                continue ;
              }

              // save connectors of cable rack
              connectors.AddRange( instance.GetConnectors() ) ;
            }

            transaction.Commit() ;
          }
          catch {
            transaction.RollBack() ;
          }
        }

        // connect all connectors
        foreach ( Connector connector in connectors ) {
          if ( ! connector.IsConnected ) {
            var otherConnectors = connectors.FindAll( x => ! x.IsConnected && x.Owner.Id != connector.Owner.Id ) ;
            if ( otherConnectors != null ) {
              var connectTo = GetConnectorClosestTo( otherConnectors, connector.Origin, maxDistanceTolerance ) ;
              if ( connectTo != null ) {
                connector.ConnectTo( connectTo ) ;
              }
            }
          }
        }
      }
    }

    /// <summary>
    /// Return the connector in the set
    /// closest to the given point.
    /// </summary>
    /// <param name="connectors"></param>
    /// <param name="point"></param>
    /// <param name="maxDistance"></param>
    /// <returns></returns>
    public static Connector? GetConnectorClosestTo( List<Connector> connectors, XYZ point,
      double maxDistance = double.MaxValue )
    {
      double minDistance = double.MaxValue ;
      Connector? targetConnector = null ;

      foreach ( Connector connector in connectors ) {
        double distance = connector.Origin.DistanceTo( point ) ;

        if ( distance < minDistance && distance <= maxDistance ) {
          targetConnector = connector ;
          minDistance = distance ;
        }
      }

      return targetConnector ;
    }

    /// <summary>
    /// Return the first connector.
    /// </summary>
    /// <param name="connectors"></param>
    /// <returns></returns>
    public static Connector? GetFirstConnector( ConnectorSet connectors )
    {
      foreach ( Connector connector in connectors ) {
        if ( 0 == connector.Id ) {
          return connector ;
        }
      }

      return null ;
    }

    /// <summary>
    /// Check cable tray exists (same place)
    /// </summary>
    /// <param name="document"></param>
    /// <param name="familyInstance"></param>
    /// <returns></returns>
    public static bool ExistsCableTray( Document document, FamilyInstance familyInstance )
    {
      return document.GetAllElements<FamilyInstance>().OfCategory( CableTrayBuiltInCategories ).OfNotElementType()
        .Where( x => IsSameLocation( x.Location, familyInstance.Location ) && x.Id != familyInstance.Id &&
                     x.FacingOrientation.IsAlmostEqualTo( familyInstance.FacingOrientation ) ).Any() ;
    }

    /// <summary>
    /// compare 2 locations
    /// </summary>
    /// <param name="location"></param>
    /// <param name="otherLocation"></param>
    /// <returns></returns>
    public static bool IsSameLocation( Location location, Location otherLocation )
    {
      if ( location is LocationPoint ) {
        if ( ! ( otherLocation is LocationPoint ) ) {
          return false ;
        }

        var locationPoint = ( location as LocationPoint )! ;
        var otherLocationPoint = ( otherLocation as LocationPoint )! ;
        return locationPoint.Point.DistanceTo( otherLocationPoint.Point) <= maxDistanceTolerance &&
               locationPoint.Rotation == otherLocationPoint.Rotation ;
      }
      else if ( location is LocationCurve ) {
        if ( ! ( otherLocation is LocationCurve ) ) {
          return false ;
        }

        var locationCurve = ( location as LocationCurve )! ;
        var line = ( locationCurve.Curve as Line )! ;

        var otherLocationCurve = ( otherLocation as LocationCurve )! ;
        var otherLine = ( otherLocationCurve.Curve as Line )! ;

        return line.Origin.IsAlmostEqualTo( otherLine.Origin, maxDistanceTolerance ) &&
               line.Direction == otherLine.Direction && line.Length == otherLine.Length ;
      }

      return location.Equals( otherLocation ) ;
    }
  }
}