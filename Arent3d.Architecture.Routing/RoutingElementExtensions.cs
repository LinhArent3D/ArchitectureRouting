using System ;
using System.Collections.Generic ;
using System.Linq ;
using Arent3d.Architecture.Routing.EndPoints ;
using Arent3d.Architecture.Routing.StorableCaches ;
using Arent3d.Revit ;
using Arent3d.Revit.I18n ;
using Arent3d.Utility ;
using Autodesk.Revit.DB ;
using Autodesk.Revit.DB.Electrical ;
using Autodesk.Revit.DB.Mechanical ;
using Autodesk.Revit.DB.Plumbing ;
using Autodesk.Revit.DB.Structure ;

namespace Arent3d.Architecture.Routing
{
  public static class RoutingElementExtensions
  {
    #region Initializations

    /// <summary>
    /// Confirms whether families and parameters used for routing application are loaded.
    /// </summary>
    /// <param name="document"></param>
    /// <returns>True if all families and parameters are loaded.</returns>
    public static bool RoutingSettingsAreInitialized( this Document document )
    {
      return document.AllRoutingFamiliesAreLoaded() && document.AllRoutingParametersAreRegistered() ;
    }

    /// <summary>
    /// Setup all families and parameters used for routing application.
    /// </summary>
    /// <param name="document"></param>
    public static bool SetupRoutingFamiliesAndParameters( this Document document )
    {
      document.MakeCertainAllRoutingFamilies() ;
      document.MakeCertainAllRoutingParameters() ;
      return document.RoutingSettingsAreInitialized() ;
    }

    /// <summary>
    /// Setup all families and parameters used for routing application.
    /// </summary>
    /// <param name="document"></param>
    public static void UnsetupRoutingFamiliesAndParameters( this Document document )
    {
      EraseArentConduitType( document ) ;
      document.EraseAllRoutingFamilies() ;
      document.UnloadAllRoutingParameters() ;
    }

    private static string GetRadius50ElbowTypeName( Document document )
    {
      return "Routing.Revit.DummyConduit.Radius50ElbowTypeName".GetDocumentStringByKeyOrDefault( document, "M_電線管エルボ - 鉄鋼 - 曲げ半径50mm - Arent" ) ;
    }
    public static string GetConduitTypeName( Document document )
    {
      return "Routing.Revit.DummyConduit.ConduitTypeName".GetDocumentStringByKeyOrDefault( document, "Arent電線" ) ;
    }

    private static double GetConduitTypeNominalDiameter( Document document )
    {
      return ( 1.0 ).MillimetersToRevitUnits() ;
    }

    /// Add new "Arent電線" conduit size setting
    private static (Element? ArentStandard, string CopyFromStandardName) AddArentConduitStandard( Document document, string newStandardName )
    {
      var sizeStandards = ConduitSizeSettings.GetConduitSizeSettings( document ) ;
      var standardNames = sizeStandards.Select( table => table.Key ).ToList() ;
      var copyFromStandard = standardNames.Last() ;

      // create "Arent電線" standard table if not existing
      if ( ! standardNames.Contains( newStandardName ) ) {
        if ( ! sizeStandards.CreateConduitStandardTypeFromExisingStandardType( document, newStandardName, copyFromStandard ) )
          return ( null, "" ) ;
      }

      // in "Arent電線" standard table, change bending radius of all sizes to 50mm by removing existing sizes and adding modified ones
      var sizeSetting = sizeStandards.SingleOrDefault( x => x.Key == newStandardName ) ;
      List<(ConduitSize OldSize, ConduitSize NewSize)> listOldNewSize = sizeSetting.Value.Select( size => ( size, new ConduitSize( size.NominalDiameter, size.InnerDiameter, size.OuterDiameter, ( 50.0 ).MillimetersToRevitUnits(), true, true ) ) ).ToList() ;

      foreach ( var pair in listOldNewSize ) {
        sizeStandards.RemoveSize( newStandardName, pair.OldSize.NominalDiameter ) ;
        sizeStandards.AddSize( newStandardName, pair.NewSize ) ;
      }

      // add a new size of 0.8 (inner diameter) to sizeSetting
      var conduitSize = new ConduitSize( GetConduitTypeNominalDiameter( document ), ( 0.8 ).MillimetersToRevitUnits(), ( 1.2 ).MillimetersToRevitUnits(), ( 16.0 ).MillimetersToRevitUnits(), true, true ) ;
      sizeStandards.RemoveSize( newStandardName, conduitSize.NominalDiameter ) ;
      sizeStandards.AddSize( newStandardName, conduitSize ) ;

      var arentConduitStandard = document.GetAllElements<ElementType>().OfCategory( BuiltInCategory.OST_ConduitStandards ).SingleOrDefault( x => x.Name == newStandardName ) ;

      return ( arentConduitStandard, copyFromStandard ) ;
    }

    public static void AddArentConduitType( Document document )
    {
      // Add new "Arent電線" conduit size setting
      var conduitTypeName = GetConduitTypeName( document ) ;
      var (arentStandard, copyFromStandardName) = AddArentConduitStandard( document, conduitTypeName ) ;

      var elbowTypeName = GetRadius50ElbowTypeName( document ) ;
      var elbowCurveType = document.GetAllElements<FamilySymbol>().OfCategory( BuiltInCategory.OST_ConduitFitting ).FirstOrDefault( x => x.FamilyName == elbowTypeName ) ;

      // Get type by "Standard" parameter
      var allConduitTypes = document.GetAllElements<ConduitType>() ;
      var curveTypes = allConduitTypes.Where( c => c.get_Parameter( BuiltInParameter.CONDUIT_STANDARD_TYPE_PARAM ).AsValueString() == copyFromStandardName ).ToList() ;
      if ( curveTypes.Count == 0 )
        curveTypes = allConduitTypes.ToList() ;
      foreach ( var curveType in curveTypes ) {
        var arentExistingConduitType = allConduitTypes.FirstOrDefault( c => c.Name == conduitTypeName && c.FamilyName == curveType.FamilyName ) ;
        var arentNewConduitType = arentExistingConduitType ?? curveType.Duplicate( conduitTypeName ) as ConduitType ;

        // Change "Bend (曲げ)" setting to "M_電線管エルボ - 鉄鋼 - 曲げ半径50mm - Arent"
        arentNewConduitType!.Elbow = elbowCurveType ;

        // Set "standard" parameter of conduit type to "Arent電線"
        if ( arentStandard == null ) continue ;
        var parameterStandard = arentNewConduitType.get_Parameter( BuiltInParameter.CONDUIT_STANDARD_TYPE_PARAM ) ;
        parameterStandard.Set( arentStandard.Id ) ;
      }
    }

    private static void EraseArentConduitType( Document document )
    {
      var conduitTypeName = GetConduitTypeName( document ) ;
      var sizes = ConduitSizeSettings.GetConduitSizeSettings( document ) ;
      var standards = document.GetStandardTypes().ToList() ;
      if ( standards.Contains( conduitTypeName ) ) {
        // todo check existence
        sizes.RemoveSize( conduitTypeName, GetConduitTypeNominalDiameter( document ) ) ;
        sizes.RemoveSize( standards.Last(), GetConduitTypeNominalDiameter( document ) ) ;
      }

      var curveTypes = document.GetAllElements<ConduitType>().Where( c => c.get_Parameter( BuiltInParameter.CONDUIT_STANDARD_TYPE_PARAM ).AsValueString() == conduitTypeName ).OfType<MEPCurveType>().ToList() ;
      
      var eraseIds = document.GetAllElements<ConduitType>().Where( curveType => curveType.Name == conduitTypeName ).Select( curveType => curveType.Id ).ToList() ;
      
      eraseIds.AddRange(curveTypes.Select(curveType => curveType.Id));
      document.Delete( eraseIds.Distinct().ToArray() ) ;
    }

    #endregion

    #region Connectors (General)

    public static Connector? FindConnector( this Document document, int elementId, int connectorId )
    {
      return document.GetElement( new ElementId( elementId ) ).GetConnectorManager()?.Lookup( connectorId ) ;
    }

    public static ConnectorManager? GetConnectorManager( this Element elm )
    {
      return elm switch
      {
        FamilyInstance fi => fi.MEPModel?.ConnectorManager,
        MEPSystem sys => sys.ConnectorManager,
        MEPCurve crv => crv.ConnectorManager,
        _ => null,
      } ;
    }

    public static IEnumerable<Connector> GetConnectors( this Element elm )
    {
      if ( ! ( elm.GetConnectorManager()?.Connectors is { } connectorSet ) ) return Array.Empty<Connector>() ;

      return connectorSet.OfType<Connector>() ;
    }

    public static ConnectorSet ToConnectorSet( this IEnumerable<Connector> connectors )
    {
      var connectorSet = new ConnectorSet() ;

      foreach ( var connector in connectors ) {
        connectorSet.Insert( connector ) ;
      }

      return connectorSet ;
    }

    /// <summary>
    /// Returns connected connectors.
    /// </summary>
    /// <param name="connector"></param>
    /// <returns></returns>
    public static IEnumerable<Connector> GetConnectedConnectors( this Connector connector )
    {
      var id = connector.Owner.Id ;
      return connector.AllRefs.OfType<Connector>().Where( c => c.Owner.Id != id ) ;
    }

    public static IEnumerable<Connector> OfEnd( this IEnumerable<Connector> connectors )
    {
      return connectors.Where( c => c.IsAnyEnd() ) ;
    }

    public static bool IsAnyEnd( this Connector conn )
    {
      return 0 != ( (int)conn.ConnectorType & (int)ConnectorType.AnyEnd ) ;
    }

    public static IEnumerable<Connector> GetOtherConnectorsInOwner( this Connector connector )
    {
      var id = connector.Id ;
      var manager = connector.ConnectorManager ;
      if ( null == manager ) return Array.Empty<Connector>() ;

      return manager.Connectors.OfType<Connector>().Where( c => c.Id != id ) ;
    }

    public static bool HasCompatibleSystemType( this Connector connector1, Connector connector2 )
    {
      if ( connector1.Domain != connector2.Domain ) return false ;

      var classification1 = connector1.GetSystemType() ;
      var classification2 = connector2.GetSystemType() ;
      if ( classification1.IsCompatibleTo( classification2 ) ) return true ;

      return false ;
    }


    public static MEPSystemClassification GetSystemType( this Connector conn )
    {
      return conn.Domain switch
      {
        Domain.DomainPiping => (MEPSystemClassification)conn.PipeSystemType,
        Domain.DomainHvac => (MEPSystemClassification)conn.DuctSystemType,
        Domain.DomainElectrical => (MEPSystemClassification)conn.ElectricalSystemType,
        Domain.DomainCableTrayConduit => MEPSystemClassification.CableTrayConduit,
        _ => MEPSystemClassification.UndefinedSystemClassification,
      } ;
    }

    public static bool HasCompatibleSystemType( this Connector connector, MEPSystemClassification systemClassification )
    {
      if ( systemClassification == MEPSystemClassification.Global || systemClassification == MEPSystemClassification.Fitting ) {
        return true ;
      }

      return systemClassification.IsCompatibleTo( connector.GetSystemType() ) ;
    }

    private static bool IsCompatibleTo( this MEPSystemClassification systemClassification1, MEPSystemClassification systemClassification2 )
    {
      if ( systemClassification1 == MEPSystemClassification.UndefinedSystemClassification ) return true ;
      if ( systemClassification2 == MEPSystemClassification.UndefinedSystemClassification ) return true ;

      if ( systemClassification1 == MEPSystemClassification.PowerCircuit && IsCompatibleToPowerCircuit( systemClassification2 ) ) return true ;
      if ( systemClassification2 == MEPSystemClassification.PowerCircuit && IsCompatibleToPowerCircuit( systemClassification1 ) ) return true ;

      return ( systemClassification1 == systemClassification2 ) ;
    }

    private static bool IsCompatibleToPowerCircuit( MEPSystemClassification systemClassification )
    {
      return ( systemClassification == MEPSystemClassification.PowerBalanced || systemClassification == MEPSystemClassification.PowerUnBalanced ) ;
    }

    public static Connector GetTopConnectorOfConnectorFamily( this FamilyInstance elm )
    {
      var topItem = elm.GetConnectors().MaxBy( conn => conn.Origin.Z ) ;
      return topItem ?? elm.GetConnectors().First() ;
    }
    public static Connector GetBottomConnectorOfConnectorFamily( this FamilyInstance elm )
    {
      var topItem = elm.GetConnectors().MinBy( conn => conn.Origin.Z ) ;
      return topItem ?? elm.GetConnectors().First() ;
    }

    public enum ConnectorPosition
    {
      Left,
      Right,
      Front,
      Back,
      Top,
      Bottom
    }
    
    public static Connector GetBottomConnectorOfConnectorFamily( this FamilyInstance elm, ConnectorPosition connectorPosition )
    {
      var connector = elm.GetConnectors().FirstOrDefault( x => x.Description.Contains( connectorPosition.GetFieldName() ) ) ?? elm.GetConnectors().First() ;
      return connector ;
    }

    #endregion

    #region Connectors (Routing)

    public static bool IsCompatibleTo( this Connector conn1, Connector conn2 )
    {
      return ( conn1.ConnectorType == conn2.ConnectorType ) && ( conn1.Domain == conn2.Domain ) && conn1.HasCompatibleShape( conn2 ) ;
    }

    public static bool HasCompatibleShape( this IConnector conn1, IConnector conn2 )
    {
      return ( conn1.Shape == conn2.Shape ) ;
    }

    public static bool HasSameShapeAndParameters( this IConnector conn1, IConnector conn2 )
    {
      if ( conn1.Shape != conn2.Shape ) return false ;

      // Concrete shape parameter can be different
      return conn1.Shape switch
      {
        ConnectorProfileType.Oval => HasSameOvalShape( conn1, conn2 ),
        ConnectorProfileType.Round => HasSameRoundShape( conn1, conn2 ),
        ConnectorProfileType.Rectangular => HasSameRectangularShape( conn1, conn2 ),
        _ => false,
      } ;
    }

    private static bool HasSameOvalShape( IConnector conn1, IConnector conn2 )
    {
      var document = GetDocument( conn1 ) ?? GetDocument( conn2 ) ?? throw new InvalidOperationException() ;
      var tole = document.Application.VertexTolerance ;
      return Math.Abs( conn1.Radius - conn2.Radius ) < tole ;
    }

    private static bool HasSameRoundShape( IConnector conn1, IConnector conn2 )
    {
      var document = GetDocument( conn1 ) ?? GetDocument( conn2 ) ?? throw new InvalidOperationException() ;
      var tole = document.Application.VertexTolerance ;
      return Math.Abs( conn1.Radius - conn2.Radius ) < tole ;
    }

    private static bool HasSameRectangularShape( IConnector conn1, IConnector conn2 )
    {
      var document = GetDocument( conn1 ) ?? GetDocument( conn2 ) ?? throw new InvalidOperationException() ;
      var tole = document.Application.VertexTolerance ;
      return Math.Abs( conn1.Width - conn2.Width ) < tole && Math.Abs( conn1.Height - conn2.Height ) < tole ;
    }

    private static Document? GetDocument( IConnector conn )
    {
      return conn switch
      {
        Connector c => c.Owner?.Document,
        Element ce => ce.Document,
        _ => null,
      } ;
    }

    #endregion

    #region Pass Points

    public static FamilyInstance? FindPassPointElement( this Document document, int elementId )
    {
      var instance = document.GetElementById<FamilyInstance>( elementId ) ;
      if ( null == instance ) return null ;

      if ( instance.Symbol.Id != document.GetFamilySymbols( RoutingFamilyType.PassPoint ).FirstOrDefault().GetValidId() ) {
        // Family instance is not a pass point.
        return null ;
      }

      return instance ;
    }

    public static void SetPassPointConnectors( this Element element, IReadOnlyCollection<Connector> fromConnectors, IReadOnlyCollection<Connector> toConnectors )
    {
      element.SetProperty( PassPointParameter.PassPointNextToFromSideConnectorUniqueIds, string.Join( "|", fromConnectors.Select( ConnectorEndPoint.BuildParameterString ) ) ) ;
      element.SetProperty( PassPointParameter.PassPointNextToToSideConnectorUniqueIds, string.Join( "|", toConnectors.Select( ConnectorEndPoint.BuildParameterString ) ) ) ;
    }

    private static readonly char[] PassPointConnectorSeparator = { '|' } ;

    public static IEnumerable<IEndPoint> GetPassPointConnectors( this Element element, bool isFrom )
    {
      var parameter = isFrom ? PassPointParameter.PassPointNextToFromSideConnectorUniqueIds : PassPointParameter.PassPointNextToToSideConnectorUniqueIds ;
      if ( false == element.TryGetProperty( parameter, out string? str ) ) return Array.Empty<IEndPoint>() ;
      if ( string.IsNullOrEmpty( str ) ) return Array.Empty<IEndPoint>() ;

      var document = element.Document ;
      return str!.Split( PassPointConnectorSeparator, StringSplitOptions.RemoveEmptyEntries ).Select( s => ConnectorEndPoint.ParseParameterString( document, s ) ).NonNull() ;
    }

    public static bool IsPassPoint( this Element element )
    {
      return element is FamilyInstance fi && fi.IsPassPoint() ;
    }

    public static bool IsPassPoint( this FamilyInstance element )
    {
      return element.IsFamilyInstanceOf( RoutingFamilyType.PassPoint ) || ( element.TryGetProperty( RoutingParameter.RelatedPassPointUniqueId, out string? uniqueId ) && null != uniqueId && null != element.Document.GetElementById<Element>( uniqueId ) ) ;
    }

    public static bool IsConnectorPoint( this FamilyInstance element )
    {
      return element.IsFamilyInstanceOfAny( RoutingFamilyType.ConnectorInPoint, RoutingFamilyType.ConnectorOutPoint, RoutingFamilyType.ConnectorPoint, RoutingFamilyType.TerminatePoint ) ;
    }

    public static string? GetPassPointUniqueId( this Element element )
    {
      if ( element is not FamilyInstance fi ) return null ;

      if ( fi.IsFamilyInstanceOf( RoutingFamilyType.PassPoint ) ) return fi.UniqueId ;
      if ( element.TryGetProperty( RoutingParameter.RelatedPassPointUniqueId, out string? id ) && false == string.IsNullOrEmpty( id ) ) return id ;
      return null ;
    }

    public static FamilyInstance AddPassPoint( this Document document, string routeName, XYZ position, XYZ direction, double? radius, ElementId levelId )
    {
      var symbol = document.GetFamilySymbols( RoutingFamilyType.PassPoint ).FirstOrDefault() ?? throw new InvalidOperationException() ;
      var instance = document.CreateFamilyInstance( symbol, position, direction, StructuralType.NonStructural, true, document.GetElementById<Level>( levelId ) ) ;
      if ( radius.HasValue ) {
        instance.LookupParameter( "Arent-RoundDuct-Diameter" ).Set( radius.Value * 2.0 ) ;
      }

      instance.SetProperty( RoutingParameter.RouteName, routeName ) ;

      return instance ;
    }

    public static FamilyInstance AddConnectorFamily( this Document document, Connector conn, string routeName, FlowDirectionType directionType, XYZ position, XYZ direction, double? radius )
    {
      var routingFamilyType = directionType switch
      {
        FlowDirectionType.In => RoutingFamilyType.ConnectorInPoint,
        FlowDirectionType.Out => RoutingFamilyType.ConnectorOutPoint,
        _ => RoutingFamilyType.ConnectorPoint,
      } ;

      var instance = document.CreateFamilyInstance( routingFamilyType, position, StructuralType.NonStructural, true, document.GetElementById<Level>( conn.Owner.LevelId ) ) ;
      var id = conn.Id ;

      instance.SetProperty( RoutingFamilyLinkedParameter.RouteConnectorRelationId, id ) ;


      var elevationAngle = Math.Atan2( direction.Z, Math.Sqrt( direction.X * direction.X + direction.Y * direction.Y ) ) ;
      Color colorIn = new Autodesk.Revit.DB.Color( (byte)255, (byte)0, (byte)0 ) ;
      Color colorOut = new Autodesk.Revit.DB.Color( (byte)0, (byte)0, (byte)255 ) ;
      OverrideGraphicSettings ogsIn = new OverrideGraphicSettings() ;
      OverrideGraphicSettings ogsOut = new OverrideGraphicSettings() ;
      ogsIn.SetProjectionLineColor( colorIn ) ;
      ogsOut.SetProjectionLineColor( colorOut ) ;

      if ( directionType == FlowDirectionType.Out ) {
        //Out
        document.ActiveView.SetElementOverrides( instance.Id, ogsIn ) ;
        if ( conn.CoordinateSystem.BasisX.Y > 0 ) {
          var rotationAngle = Math.Atan2( -direction.Y, direction.X ) ;

          ElementTransformUtils.RotateElement( document, instance.Id, Line.CreateBound( position, position + XYZ.BasisZ ), rotationAngle ) ;
        }
        else {
          var rotationAngle = Math.Atan2( direction.Y, direction.X ) ;
          ElementTransformUtils.RotateElement( document, instance.Id, Line.CreateBound( position, position + XYZ.BasisZ ), rotationAngle ) ;
        }
      }
      else if ( directionType == FlowDirectionType.In ) {
        //In
        document.ActiveView.SetElementOverrides( instance.Id, ogsOut ) ;
        if ( conn.CoordinateSystem.BasisX.Y > 0 ) {
          var rotationAngle = Math.Atan2( direction.Y, direction.X ) ;
          ElementTransformUtils.RotateElement( document, instance.Id, Line.CreateBound( position, position + XYZ.BasisZ ), rotationAngle ) ;
        }
        else {
          var rotationAngle = Math.Atan2( -direction.Y, direction.X ) ;
          ElementTransformUtils.RotateElement( document, instance.Id, Line.CreateBound( position, position + XYZ.BasisZ ), rotationAngle ) ;
        }
      }

      instance.SetProperty( RoutingParameter.RouteName, routeName ) ;

      return instance ;
    }

    public static FamilyInstance AddRackGuide( this Document document, XYZ position, Level? level )
    {
      return document.CreateFamilyInstance( RoutingFamilyType.RackGuide, position, StructuralType.NonStructural, true, level ) ;
    }
    public static FamilyInstance AddRackSpace( this Document document, XYZ position, Level level )
    {
      return document.CreateFamilyInstance( RoutingFamilyType.RackSpace, position, StructuralType.NonStructural, true, level ) ;
    }
    public static FamilyInstance AddFASU(this Document document, MechanicalRoutingFamilyType fasuFamilyType, XYZ position, ElementId levelId)
    {
      return document.CreateFamilyInstance( fasuFamilyType, position, StructuralType.NonStructural, true, document.GetElementById<Level>(levelId));
    }
    public static FamilyInstance AddVAV(this Document document, XYZ position, ElementId levelId)
    {
      return document.CreateFamilyInstance( MechanicalRoutingFamilyType.SA_VAV, position, StructuralType.NonStructural, true, document.GetElementById<Level>(levelId));
    }
    public static FamilyInstance AddShaft( this Document document, XYZ position, Level? level )
    {
      return document.CreateFamilyInstance( RoutingFamilyType.Shaft, position, StructuralType.NonStructural, true, level ) ;
    }

    #endregion

    #region Terminate Points

    public static FamilyInstance? FindTerminatePointElement( this Document document, int elementId )
    {
      var instance = document.GetElementById<FamilyInstance>( elementId ) ;
      if ( null == instance ) return null ;

      if ( instance.Symbol.Id != document.GetFamilySymbols( RoutingFamilyType.TerminatePoint ).FirstOrDefault()?.Id ) {
        // Family instance is not a pass point.
        return null ;
      }

      return instance ;
    }

    public static bool IsTerminatePoint( this Element element )
    {
      return element is FamilyInstance fi && fi.IsTerminatePoint() ;
    }

    public static bool IsTerminatePoint( this FamilyInstance element )
    {
      return element.IsFamilyInstanceOf( RoutingFamilyType.TerminatePoint ) || ( element.TryGetProperty( RoutingParameter.RelatedTerminatePointUniqueId, out string? uniqueId ) && null != uniqueId && null != element.Document.GetElementById<Element>( uniqueId ) ) ;
    }

    public static string? GetTerminatePointUniqueId( this Element element )
    {
      if ( element is not FamilyInstance fi ) return null ;

      if ( fi.IsFamilyInstanceOf( RoutingFamilyType.TerminatePoint ) ) return fi.UniqueId ;
      if ( element.TryGetProperty( RoutingParameter.RelatedTerminatePointUniqueId, out string? id ) && false == string.IsNullOrEmpty( id ) ) return id ;
      return null ;
    }

    public static FamilyInstance AddTerminatePoint( this Document document, string routeName, XYZ position, XYZ direction, double? radius, ElementId levelId )
    {
      var symbol = document.GetFamilySymbols( RoutingFamilyType.TerminatePoint ).FirstOrDefault() ?? throw new InvalidOperationException() ;
      var instance = document.CreateFamilyInstance( symbol, position, direction, StructuralType.NonStructural, true, document.GetElementById<Level>( levelId ) ) ;
      if ( radius.HasValue ) {
        instance.LookupParameter( "Arent-RoundDuct-Diameter" ).Set( radius.Value * 2.0 ) ;
      }

      instance.SetProperty( RoutingParameter.RouteName, routeName ) ;

      return instance ;
    }

    #endregion

    #region Routing (General)

    public static bool IsAutoRoutingGeneratedElement( this Element element )
    {
      return element.IsAutoRoutingGeneratedElementType() && element.TryGetProperty( RoutingParameter.RouteName, out string? routeName ) && false == string.IsNullOrEmpty( routeName ) ;
    }

    public static bool IsAutoRoutingGeneratedElementType( this Element element )
    {
      return element switch
      {
        Duct or Pipe or CableTray => true,
        _ => IsFittingElement( element ),
      } ;
    }

    public static bool IsFittingElement( this Element element )
    {
      var category = element.Category ;
      if ( null == category ) return false ;
      return ( category.CategoryType == CategoryType.Model && IsFittingCategory( category.GetBuiltInCategory() ) ) ;
    }

    private static bool IsFittingCategory( BuiltInCategory category )
    {
      return ( 0 <= Array.IndexOf( BuiltInCategorySets.Fittings, category ) ) ;
    }

    #endregion

    #region Routing (Route Names)

    public static IEnumerable<TElement> GetAllElementsOfRoute<TElement>( this Document document ) where TElement : Element
    {
      var parameterName = document.GetParameterName( RoutingParameter.RouteName ) ;
      if ( null == parameterName ) return Array.Empty<TElement>() ;

      var filter = new ElementParameterFilter( ParameterFilterRuleFactory.CreateSharedParameterApplicableRule( parameterName ) ) ;

      return document.GetAllElementsOfRouteName<TElement>( BuiltInCategorySets.RoutingElements, filter ) ;
    }

    public static IEnumerable<TElement> GetAllElementsOfRouteName<TElement>( this Document document, string routeName ) where TElement : Element
    {
      var parameterName = document.GetParameterName( RoutingParameter.RouteName ) ;
      if ( null == parameterName ) return Array.Empty<TElement>() ;

      var filter = new ElementParameterFilter( ParameterFilterRuleFactory.CreateSharedParameterApplicableRule( parameterName ) ) ;

      return document.GetAllElementsOfRouteName<TElement>( BuiltInCategorySets.RoutingElements, filter ).Where( e => e.GetRouteName() == routeName ) ;
    }

    public static IEnumerable<TElement> GetAllElementsOfRepresentativeRouteName<TElement>( this Document document, string routeName ) where TElement : Element
    {
      var parameterName = document.GetParameterName( RoutingParameter.RepresentativeRouteName ) ;
      if ( null == parameterName ) return Array.Empty<TElement>() ;

      var filter = new ElementParameterFilter( ParameterFilterRuleFactory.CreateSharedParameterApplicableRule( parameterName ) ) ;

      return document.GetAllElementsOfRouteName<TElement>( BuiltInCategorySets.RoutingElements, filter ).Where( e => e.GetRepresentativeRouteName() == routeName ) ;
    }

    public static IEnumerable<TElement> GetAllElementsOfSubRoute<TElement>( this Document document, string routeName, int subRouteIndex ) where TElement : Element
    {
      var routeNameParameterName = document.GetParameterName( RoutingParameter.RouteName ) ;
      if ( null == routeNameParameterName ) return Array.Empty<TElement>() ;

      var subRouteIndexParameterName = document.GetParameterName( RoutingParameter.SubRouteIndex ) ;
      if ( null == subRouteIndexParameterName ) return Array.Empty<TElement>() ;

      var filter = new ElementParameterFilter( new[] { ParameterFilterRuleFactory.CreateSharedParameterApplicableRule( routeNameParameterName ), ParameterFilterRuleFactory.CreateSharedParameterApplicableRule( subRouteIndexParameterName ), } ) ;

      return document.GetAllElementsOfRouteName<TElement>( BuiltInCategorySets.RoutingElements, filter ).Where( e => e.GetRouteName() == routeName ).Where( e => e.GetSubRouteIndex() == subRouteIndex ) ;
    }

    private static IEnumerable<TElement> GetAllElementsOfRouteName<TElement>( this Document document, BuiltInCategory[] builtInCategories, ElementFilter filter ) where TElement : Element
    {
      return document.GetAllElements<Element>().OfCategory( builtInCategories ).OfNotElementType().Where( filter ).OfType<TElement>() ;
    }

    public static IEnumerable<FamilyInstance> GetAllElementsOfPassPoint( this Document document, string passPointUniqueId )
    {
      var parameterName = document.GetParameterName( RoutingParameter.RelatedPassPointUniqueId ) ;
      if ( null == parameterName ) yield break ;

      var elm = document.GetElementById<FamilyInstance>( passPointUniqueId ) ;
      if ( null == elm ) yield break ;
      if ( elm.IsFamilyInstanceOf( RoutingFamilyType.PassPoint ) ) yield return elm ;

      var filter = new ElementParameterFilter( ParameterFilterRuleFactory.CreateSharedParameterApplicableRule( parameterName ) ) ;

      foreach ( var e in document.GetAllElements<Element>().OfCategory( BuiltInCategorySets.PassPoints ).OfNotElementType().Where( filter ).OfType<FamilyInstance>() ) {
        if ( e.IsFamilyInstanceOf( RoutingFamilyType.PassPoint ) ) continue ;
        if ( e.TryGetProperty( RoutingParameter.RelatedPassPointUniqueId, out string? id ) && id == passPointUniqueId ) yield return e ;
      }
    }

    public static string? GetRouteName( this Element element )
    {
      if ( false == element.TryGetProperty( RoutingParameter.RouteName, out string? value ) ) return null ;
      return value ;
    }

    public static int? GetSubRouteIndex( this Element element )
    {
      if ( false == element.TryGetProperty( RoutingParameter.SubRouteIndex, out int value ) ) return null ;
      return value ;
    }

    public static SubRouteInfo? GetSubRouteInfo( this Element element )
    {
      if ( element.GetRouteName() is not { } routeName ) return null ;
      if ( element.GetSubRouteIndex() is not { } subRouteIndex ) return null ;

      return new SubRouteInfo( routeName, subRouteIndex ) ;
    }

    public static IEnumerable<Connector> CollectRoutingEndPointConnectors( this Document document, string routeName, bool fromConnector )
    {
      return document.GetAllElementsOfRouteName<MEPCurve>( routeName ).SelectMany( e => e.GetRoutingConnectors( fromConnector ) ).Distinct() ;
    }

    public static (IReadOnlyCollection<Connector> From, IReadOnlyCollection<Connector>To) GetConnectors( this Document document, string routeName )
    {
      var fromList = document.CollectRoutingEndPointConnectors( routeName, true ).EnumerateAll() ;
      var toList = document.CollectRoutingEndPointConnectors( routeName, false ).EnumerateAll() ;
      return ( From: fromList, To: toList ) ;
    }

    public static Dictionary<string, List<MEPCurve>> CollectAllMultipliedRoutingElements( this Document document, int multiplicity )
    {
      return document.CollectAllMultipliedRoutingElements( document.GetAllElementsOfRoute<MEPCurve>(), multiplicity ) ;
    }

    public static Dictionary<string, List<MEPCurve>> CollectAllMultipliedRoutingElements( this Document document, IEnumerable<MEPCurve> mepCurves, int multiplicity )
    {
      if ( multiplicity < 2 ) throw new ArgumentOutOfRangeException( nameof( multiplicity ) ) ;

      var routingElementsGroupByRouteName = new Dictionary<string, List<MEPCurve>>() ;
      var routes = RouteCache.Get( DocumentKey.Get( document ) ) ;

      foreach ( var mepCurve in mepCurves ) {
        if ( mepCurve?.GetSubRouteInfo() is not { } subRouteInfo ) continue ;
        if ( routes.GetSubRoute( subRouteInfo ) == null ) continue ;
        if ( mepCurve is not Conduit conduit ) continue ;

        var fromEndPoint = conduit.GetNearestEndPoints( true ).FirstOrDefault() ;
        var toEndPoint = conduit.GetNearestEndPoints( false ).FirstOrDefault() ;
        if ( fromEndPoint == null || toEndPoint == null ) continue ;

        var key = fromEndPoint.Key.GetElementUniqueId() + "_" + toEndPoint.Key.GetElementUniqueId() ;

        if ( routingElementsGroupByRouteName.ContainsKey( key ) )
          routingElementsGroupByRouteName[ key ].Add( mepCurve ) ;
        else
          routingElementsGroupByRouteName.Add( key, new List<MEPCurve> { mepCurve } ) ;
      }

      return routingElementsGroupByRouteName.Where( p =>
        p.Value.Select( s => s.GetRouteName() ).Where( r => ! string.IsNullOrEmpty( r ) ).Distinct().Count() >= multiplicity )
        .ToDictionary( p => p.Key, p => p.Value ) ;
    }

    #endregion

    #region Routing (From-To)

    public static void SetRoutedElementFromToConnectorIds( this Element element, IReadOnlyCollection<int> fromIds, IReadOnlyCollection<int> toIds )
    {
      element.SetProperty( RoutingParameter.RoutedElementFromSideConnectorIds, string.Join( "|", fromIds ) ) ;
      element.SetProperty( RoutingParameter.RoutedElementToSideConnectorIds, string.Join( "|", toIds ) ) ;
    }

    public static IReadOnlyCollection<Connector> GetRoutingConnectors( this Element element, bool isFrom )
    {
      var manager = element.GetConnectorManager() ;
      if ( null == manager ) return Array.Empty<Connector>() ;

      var routingParam = ( isFrom ? RoutingParameter.RoutedElementFromSideConnectorIds : RoutingParameter.RoutedElementToSideConnectorIds ) ;
      if ( false == element.TryGetProperty( routingParam, out string? value ) ) return Array.Empty<Connector>() ;
      if ( null == value ) return Array.Empty<Connector>() ;

      var list = new List<Connector>() ;
      foreach ( var s in value.Split( '|' ) ) {
        if ( false == int.TryParse( s, out var id ) ) continue ;

        var conn = manager.Lookup( id ) ;
        if ( null == conn ) continue ;

        list.Add( conn ) ;
      }

      return list ;
    }

    public static bool IsRoutingConnector( this Connector connector, bool isFrom )
    {
      var routingParam = ( isFrom ? RoutingParameter.RoutedElementFromSideConnectorIds : RoutingParameter.RoutedElementToSideConnectorIds ) ;
      if ( false == connector.Owner.TryGetProperty( routingParam, out string? value ) ) return false ;
      if ( null == value ) return false ;

      var targetId = connector.Id ;
      return value.Split( '|' ).Any( s => int.TryParse( s, out var id ) && id == targetId ) ;
    }

    public static IEnumerable<Route> CollectRoutes( this Document document, AddInType addInType )
    {
      var routes = RouteCache.Get( DocumentKey.Get( document ) ).Values ;

      return addInType switch
      {
        AddInType.Electrical => routes.Where( r => r.GetSystemClassificationInfo().AddInType == AddInType.Electrical ),
        AddInType.Mechanical => routes.Where( r => r.GetSystemClassificationInfo().AddInType == AddInType.Mechanical ),
        AddInType.Undefined => routes.Where( r => r.GetSystemClassificationInfo().AddInType == AddInType.Undefined ),
        _ => routes.Where( r => r.GetSystemClassificationInfo().AddInType == AddInType.Undefined )
      } ;
    }

    public static IEnumerable<IEndPoint> GetNearestEndPoints( this Element element, bool isFrom )
    {
      if ( false == element.TryGetProperty( isFrom ? RoutingParameter.NearestFromSideEndPoints : RoutingParameter.NearestToSideEndPoints, out string? str ) ) {
        return Array.Empty<IEndPoint>() ;
      }

      if ( null == str ) {
        return Array.Empty<IEndPoint>() ;
      }

      return element.Document.ParseEndPoints( str ) ;
    }

    public static IReadOnlyCollection<SubRoute> GetSubRouteGroup( this Element element )
    {
      if ( ( element.GetRepresentativeSubRoute() ?? element.GetSubRouteInfo() ) is not { } subRouteInfo ) return Array.Empty<SubRoute>() ;

      var routeCache = RouteCache.Get( DocumentKey.Get( element.Document ) ) ;
      if ( routeCache.GetSubRoute( subRouteInfo ) is not { } subRoute ) return Array.Empty<SubRoute>() ;

      var subRouteGroup = subRoute.GetSubRouteGroup() ;
      if ( subRouteGroup.Count < 2 ) return new[] { subRoute } ;

      var result = new List<SubRoute>( subRouteGroup.Count ) ;
      result.AddRange( subRouteGroup.Select( routeCache.GetSubRoute ).NonNull() ) ;
      return result ;
    }

    public static void SetRepresentativeSubRoute( this Element element, SubRouteInfo subRouteInfo )
    {
      element.SetProperty( RoutingParameter.RepresentativeRouteName, subRouteInfo.RouteName ) ;
      element.SetProperty( RoutingParameter.RepresentativeSubRouteIndex, subRouteInfo.SubRouteIndex ) ;
    }

    public static string? GetRepresentativeRouteName( this Element element )
    {
      if ( false == element.TryGetProperty( RoutingParameter.RepresentativeRouteName, out string? value ) ) return null ;
      return value ;
    }

    public static SubRouteInfo? GetRepresentativeSubRoute( this Element element )
    {
      if ( element.GetRepresentativeRouteName() is not { } routeName ) return null ;
      if ( false == element.TryGetProperty( RoutingParameter.RepresentativeSubRouteIndex, out int subRouteIndex ) ) return null ;

      return new SubRouteInfo( routeName, subRouteIndex ) ;
    }

    /// <summary>
    /// 分岐管(Tee, Cross)のBranch側のRoute名を取得する
    /// </summary>
    /// <param name="elemnt"></param>
    /// <returns></returns>
    public static IEnumerable<string> GetBranchRouteNames( this Element element )
    {
      if ( false == element.TryGetProperty( RoutingParameter.BranchRouteNames, out string? str )) yield break;
      if ( str == null ) yield break;

      foreach ( var routeName in RouteNamesUtil.ParseRouteNames( str ) ) {
        yield return routeName ;
      }
    }
    
    #endregion

    #region Center Lines

    private static readonly ElementFilter CenterLineFilter = new ElementMulticategoryFilter( BuiltInCategorySets.CenterLineCategories ) ;

    public static IEnumerable<Element> GetCenterLine( this Element element )
    {
      var document = element.Document ;
      return element.GetDependentElements( CenterLineFilter ).Select( document.GetElement ).Where( e => e.IsValidObject ) ;
    }

    #endregion

    #region Shafts

    public static XYZ GetShaftPosition( this Opening shaft )
    {
      var box = shaft.get_BoundingBox( null ) ;
      return ( box.Min + box.Max ) * 0.5 ;
    }

    #endregion

    #region General

    private static FamilyInstance CreateFamilyInstance( this Document document, RoutingFamilyType familyType, XYZ position, StructuralType structuralType, bool useLevel, Level? level )
    {
      var symbol = document.GetFamilySymbols( familyType ).FirstOrDefault() ?? throw new InvalidOperationException() ;
      return document.CreateFamilyInstance( symbol, position, null, structuralType, useLevel, level ) ;
    }

    private static FamilyInstance CreateFamilyInstance( this Document document, ElectricalRoutingFamilyType familyType, XYZ position, StructuralType structuralType, bool useLevel, Level? level )
    {
      var symbol = document.GetFamilySymbols( familyType ).FirstOrDefault() ?? throw new InvalidOperationException() ;
      return document.CreateFamilyInstance( symbol, position, null, structuralType, useLevel, level ) ;
    }

    private static FamilyInstance CreateFamilyInstance( this Document document, MechanicalRoutingFamilyType familyType, XYZ position, StructuralType structuralType, bool useLevel, Level? level )
    {
      var symbol = document.GetFamilySymbols( familyType ).FirstOrDefault() ?? throw new InvalidOperationException() ;
      return document.CreateFamilyInstance( symbol, position, null, structuralType, useLevel, level ) ;
    }

    private static FamilyInstance CreateFamilyInstance( this Document document, FamilySymbol symbol, XYZ position, XYZ? direction, StructuralType structuralType, bool useLevel, Level? level )
    {
      if ( false == symbol.IsActive ) {
        symbol.Activate() ;
      }

      if ( false == useLevel ) {
        return document.Create.NewFamilyInstance( position, symbol, structuralType ) ;
      }

      level ??= document.GuessLevel( position ) ;
      var instance = document.Create.NewFamilyInstance( position, symbol, level, structuralType ) ;
      instance.get_Parameter( BuiltInParameter.INSTANCE_ELEVATION_PARAM ).Set( 0.0 ) ;

      if ( null != direction ) {
        var elevationAngle = Math.Atan2( direction.Z, Math.Sqrt( direction.X * direction.X + direction.Y * direction.Y ) ) ;
        var rotationAngle = Math.Atan2( direction.Y, direction.X ) ;

        ElementTransformUtils.RotateElement( document, instance.Id, Line.CreateBound( position, position + XYZ.BasisY ), -elevationAngle ) ;
        ElementTransformUtils.RotateElement( document, instance.Id, Line.CreateBound( position, position + XYZ.BasisZ ), rotationAngle ) ;
      }

      document.Regenerate() ;
      ElementTransformUtils.MoveElement( document, instance.Id, position - instance.GetTotalTransform().Origin ) ;

      return instance ;
    }
    
    private static readonly (BuiltInParameter, BuiltInParameter)[] LevelBuiltInParameterPairs = { ( BuiltInParameter.RBS_START_LEVEL_PARAM, BuiltInParameter.RBS_END_LEVEL_PARAM ) } ;

    public static ElementId GetLevelId( this Element element )
    {
      var levelId = element.LevelId ;
      if ( ElementId.InvalidElementId != levelId ) return levelId ;

      return LevelBuiltInParameterPairs.Select( tuple => GetUniqueParamLevelId( element, tuple.Item1, tuple.Item2 ) ).FirstOrDefault( paramLevelId => ElementId.InvalidElementId != paramLevelId ) ?? ElementId.InvalidElementId ;

      static ElementId GetUniqueParamLevelId( Element element, BuiltInParameter startParameter, BuiltInParameter endParameter )
      {
        var startLevelId = GetParamLevelId( element, startParameter ) ;
        var endLevelId = GetParamLevelId( element, endParameter ) ;
        if ( ElementId.InvalidElementId == startLevelId ) return ElementId.InvalidElementId ; // No levels
        if ( ElementId.InvalidElementId != endLevelId && startLevelId != endLevelId ) return ElementId.InvalidElementId ; // Different levels
        return startLevelId ;
      }

      static ElementId GetParamLevelId( Element element, BuiltInParameter builtInParameter )
      {
        if ( element.get_Parameter( builtInParameter ) is not { StorageType: StorageType.ElementId, HasValue: true } param ) return ElementId.InvalidElementId ;
        var elmId = param.AsElementId() ?? ElementId.InvalidElementId ;
        if ( ElementId.InvalidElementId == elmId ) return ElementId.InvalidElementId ;

        return element.Document.GetElementById<Level>( elmId )?.Id ?? ElementId.InvalidElementId ;
      }
    }

    public static ElementId GuessLevelId( this Document document, XYZ position )
    {
      return GuessLevel( document, position ).Id ;
    }

    public static Level GuessLevel( this Document document, XYZ position )
    {
      var z = position.Z - document.Application.VertexTolerance ;
      var list = document.GetAllElements<Level>().Select( level => new LevelByElevation( level.Elevation, level ) ).ToList() ;
      if ( 0 == list.Count ) Level.Create( document, 0 ) ;

      list.Sort() ;

      var index = list.BinarySearch( new LevelByElevation( z, null ) ) ;
      if ( 0 <= index ) return list[ index ].Level! ;

      var greaterIndex = ~index ;
      return list[ Math.Max( 0, greaterIndex - 1 ) ].Level! ;
    }

    private record LevelByElevation( double LevelElevation, Level? Level ) : IComparable<LevelByElevation>, IComparable
    {
      public int CompareTo( LevelByElevation? other )
      {
        if ( ReferenceEquals( this, other ) ) return 0 ;
        if ( ReferenceEquals( null, other ) ) return 1 ;
        return LevelElevation.CompareTo( other.LevelElevation ) ;
      }

      public int CompareTo( object? obj )
      {
        if ( ReferenceEquals( null, obj ) ) return 1 ;
        if ( ReferenceEquals( this, obj ) ) return 0 ;

        return obj is LevelByElevation other ? CompareTo( other ) : throw new ArgumentException( $"Object must be of type {nameof( LevelByElevation )}" ) ;
      }
    }

    #endregion

    #region Envelope

    public static string? GetParentEnvelopeId( this FamilyInstance familyInstance )
    {
      if ( false == familyInstance.TryGetProperty( "Revit.Property.Builtin.ParentEnvelopeId".GetDocumentStringByKeyOrDefault( familyInstance.Document, "Parent Envelope Id" ), out string? envelopeUniqueId ) || string.IsNullOrEmpty( envelopeUniqueId ) ) return null ;
      return envelopeUniqueId ;
    }

    #endregion
    
    #region Schedule

    public static void AddImageToImageMap( this ViewSchedule viewSchedule, int row, int column, ElementId imageId )
    {
      if ( ! viewSchedule.TryGetProperty( ElectricalRoutingElementParameter.ImageCellMap, out string? imageMap ) ) return ;
      imageMap ??= string.Empty ;
      imageMap += ( imageMap == string.Empty ? imageMap : "|" ) + $"{row},{column},{imageId.IntegerValue}" ;
      viewSchedule.TrySetProperty( ElectricalRoutingElementParameter.ImageCellMap, imageMap ) ;
    }

    public static void SetImageMap( this ViewSchedule viewSchedule, Dictionary<(int row, int column), ElementId> imageMap )
    {
      var imageMapString = string.Empty ;
      foreach ( var imageMapKey in imageMap.Keys ) {
        imageMapString += ( imageMapString == string.Empty ? imageMapString : "|" ) + $"{imageMapKey.row},{imageMapKey.column},{imageMap[ imageMapKey ].IntegerValue}" ;
      }

      viewSchedule.TrySetProperty( ElectricalRoutingElementParameter.ImageCellMap, imageMapString ) ;
    }

    public static (Dictionary<(int row, int column), ElementId> firstImageMap, Dictionary<(int row, int column), ElementId> secondImageMap) SplitImageMap( this ViewSchedule viewSchedule, int secondTopRow, int secondBottomRow, int headerRowCount )
    {
      var imageMap = viewSchedule.GetImageMap() ;
      var firstImageMap = new Dictionary<(int row, int column), ElementId>() ;
      var secondImageMap = new Dictionary<(int row, int column), ElementId>() ;
      foreach ( var key in imageMap.Keys ) {
        if ( key.row < headerRowCount ) //header
        {
          firstImageMap.Add( key, imageMap[ key ] ) ;
          secondImageMap.Add( key, imageMap[ key ] ) ;
        }
        else if ( key.row >= secondTopRow && key.row <= secondBottomRow ) {
          secondImageMap.Add( ( key.row - secondTopRow + headerRowCount, key.column ), imageMap[ key ] ) ;
        }
        else {
          firstImageMap.Add( key, imageMap[ key ] ) ;
        }
      }

      return ( firstImageMap, secondImageMap ) ;
    }

    public static Dictionary<(int row, int column), ElementId> GetImageMap( this ViewSchedule viewSchedule )
    {
      if ( ! viewSchedule.TryGetProperty( ElectricalRoutingElementParameter.ImageCellMap, out string? imageMap ) || string.IsNullOrEmpty( imageMap ) ) return new Dictionary<(int row, int column), ElementId>() ;
      var imageMapDictionary = new Dictionary<(int row, int column), ElementId>() ;
      string[]? imageCells = imageMap?.Split( '|' ) ;
      if ( imageCells == null ) return new Dictionary<(int row, int column), ElementId>() ;
      foreach ( var cell in imageCells ) {
        if ( string.IsNullOrEmpty( cell ) ) continue ;
        var cellItems = cell.Split( ',' ) ;
        if ( cellItems.Count() != 3 ) continue ;
        if ( ! int.TryParse( cellItems[ 0 ], out int row ) ) continue ;
        if ( ! int.TryParse( cellItems[ 1 ], out int column ) ) continue ;
        if ( ! int.TryParse( cellItems[ 2 ], out int elementIdValue ) ) continue ;
        if ( imageMapDictionary.ContainsKey( ( row, column ) ) ) continue ;
        imageMapDictionary.Add( ( row, column ), new ElementId( elementIdValue ) ) ;
      }

      return imageMapDictionary ;
    }

    public static void SetSplitStatus( this ViewSchedule viewSchedule, bool isSplit )
    {
      viewSchedule.TrySetProperty( ElectricalRoutingElementParameter.IsSplit, isSplit ? 1 : 0 ) ;
    }

    public static bool GetSplitStatus( this ViewSchedule viewSchedule )
    {
      if ( ! viewSchedule.TryGetProperty( ElectricalRoutingElementParameter.IsSplit, out int status ) ) return false ;
      return status == 1 ;
    }

    public static void SetSplitIndex( this ViewSchedule viewSchedule, int index )
    {
      viewSchedule.TrySetProperty( ElectricalRoutingElementParameter.SplitIndex, index ) ;
    }

    public static int GetSplitIndex( this ViewSchedule viewSchedule )
    {
      return ! viewSchedule.TryGetProperty( ElectricalRoutingElementParameter.SplitIndex, out int index ) ? 0 : index ;
    }

    public static void SetSplitLevel( this ViewSchedule viewSchedule, int index )
    {
      viewSchedule.TrySetProperty( ElectricalRoutingElementParameter.SplitLevel, index ) ;
    }

    public static int GetSplitLevel( this ViewSchedule viewSchedule )
    {
      return ! viewSchedule.TryGetProperty( ElectricalRoutingElementParameter.SplitLevel, out int index ) ? 0 : index ;
    }

    public static void SetParentScheduleId( this ViewSchedule viewSchedule, ElementId elementId )
    {
      viewSchedule.TrySetProperty( ElectricalRoutingElementParameter.ParentScheduleId, elementId.IntegerValue ) ;
    }

    public static ElementId? GetParentScheduleId( this ViewSchedule viewSchedule )
    {
      if ( ! viewSchedule.TryGetProperty( ElectricalRoutingElementParameter.ParentScheduleId, out int elementIdValue ) ) return null ;
      return elementIdValue == 0? null: new ElementId( elementIdValue ) ;
    }
    
    public static string GetScheduleBaseName( this ViewSchedule viewSchedule )
    {
      if ( ! viewSchedule.TryGetProperty( ElectricalRoutingElementParameter.ScheduleBaseName, out string? scheduleBaseName ) ) return string.Empty ;
      return scheduleBaseName ?? string.Empty ;
    }


    public static void SetScheduleHeaderRowCount( this ViewSchedule viewSchedule, int headerRowCount )
    {
      viewSchedule.TrySetProperty( ElectricalRoutingElementParameter.ScheduleHeaderRowCount, headerRowCount ) ;
    }

    public static int GetScheduleHeaderRowCount( this ViewSchedule viewSchedule )
    {
      if ( ! viewSchedule.TryGetProperty( ElectricalRoutingElementParameter.ScheduleHeaderRowCount, out int headerRowCount ) ) return 0 ;
      return headerRowCount ;
    }

    #endregion
  }
}