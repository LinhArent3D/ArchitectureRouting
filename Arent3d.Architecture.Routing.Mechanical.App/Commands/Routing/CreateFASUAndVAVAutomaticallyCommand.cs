﻿using Arent3d.Revit.UI ;
using Autodesk.Revit.Attributes ;
using Autodesk.Revit.DB ;
using Autodesk.Revit.UI ;
using System.Collections.Generic ;
using ImageType = Arent3d.Revit.UI.ImageType ;
using Arent3d.Architecture.Routing.AppBase ;
using Arent3d.Revit ;
using System.Linq ;
using Autodesk.Revit.DB.Mechanical ;

namespace Arent3d.Architecture.Routing.Mechanical.App.Commands.Routing
{
  [Transaction( TransactionMode.Manual )]
  [DisplayNameKey( "Mechanical.App.Commands.Routing.CreateFASUAndVAVAutomaticallyCommand", DefaultString = "Create FASU\nAnd VAV" )]
  [Image( "resources/Initialize-16.bmp", ImageType = ImageType.Normal )]
  [Image( "resources/Initialize-32.bmp", ImageType = ImageType.Large )]
  public class CreateFASUAndVAVAutomaticallyCommand : IExternalCommand
  {
    private const string CommandName = "FASU, VAV自動配置" ;

    public Result Execute( ExternalCommandData commandData, ref string message, ElementSet elements )
    {
      var uiDocument = commandData.Application.ActiveUIDocument ;
      var document = uiDocument.Document ;

      var executor = CreateRoutingExecutor( document, commandData.View ) ;

      try {
        bool success ;
        object? state ;
        ( success, state ) = OperateUI( uiDocument, executor ) ;
        if ( state is string mes ) {
          message = mes ;
        }

        if ( success ) {
          return Result.Succeeded ;
        }

        return Result.Failed ;
      }
      catch ( Autodesk.Revit.Exceptions.OperationCanceledException ) {
        return Result.Cancelled ;
      }
    }

    private static IList<Element> GetAllSpaces( Document document )
    {
      ElementCategoryFilter filter = new(BuiltInCategory.OST_MEPSpaces) ;
      FilteredElementCollector collector = new(document) ;
      IList<Element> spaces = collector.WherePasses( filter ).WhereElementIsNotElementType().ToElements() ;
      return spaces ;
    }

    bool HasBoundingBox( Element elm )
    {
      return elm.get_BoundingBox( elm.Document.ActiveView ) != null ;
    }

    private (bool Result, object? State) OperateUI( UIDocument uiDocument, RoutingExecutor routingExecutor )
    {
      IList<Element> spaces = GetAllSpaces( uiDocument.Document ).Where( space => space.HasParameter( BranchNumberParameter.BranchNumber ) ).ToArray() ;

      foreach ( var space in spaces ) {
        if ( ! HasBoundingBox( space ) ) {
          return ( false, $"`{space.Name}` doesn't have bounding box." ) ;
        }
      }

      if ( ! RoundDuctTypeExists( uiDocument.Document ) ) return ( false, "There is no RoundDuct family in the document." ) ;

      ConnectorPicker.IPickResult iPickResult = ConnectorPicker.GetConnector( uiDocument, routingExecutor, true, "Dialog.Commands.Routing.CreateFASUAndVAVAutomaticallyCommand.PickConnector", null, GetAddInType() ) ;
      if ( iPickResult.PickedConnector != null && CreateFASUAndVAVAutomatically( uiDocument.Document, iPickResult.PickedConnector, spaces ) == Result.Succeeded ) {
        TaskDialog.Show( CommandName, "FASUとVAVを配置しました。" ) ;
      }

      return ( true, null ) ;
    }

    private AddInType GetAddInType() => AppCommandSettings.AddInType ;

    private RoutingExecutor CreateRoutingExecutor( Document document, View view ) => AppCommandSettings.CreateRoutingExecutor( document, view ) ;

    private static Result CreateFASUAndVAVAutomatically( Document document, Connector pickedConnector, IList<Element> spaces )
    {
      var maintainer = new FASUAndVAVMaintainerForTTE() ;
      var (error, errorMessage) = maintainer.Setup( document, pickedConnector.CoordinateSystem.BasisZ.To3dDirection() ) ;

      if ( error ) {
        TaskDialog.Show( CommandName, errorMessage ) ;
        return Result.Failed ;
      }

      using var tr = new Transaction( document ) ;
      tr.Start( "Create FASUs and VAVs Automatically" ) ;
      maintainer.Execute( pickedConnector.Origin.To3dPoint(), pickedConnector.CoordinateSystem.BasisZ.To3dDirection(), pickedConnector.Origin.Z ) ;
      tr.Commit() ;

      return Result.Succeeded ;
    }

    private static bool RoundDuctTypeExists( Document document )
    {
      var collector = new FilteredElementCollector( document ).OfClass( typeof( DuctType ) ).AsEnumerable().OfType<DuctType>() ;
      return collector.Any( e => e.Shape == ConnectorProfileType.Round ) ;
    }
  }
}