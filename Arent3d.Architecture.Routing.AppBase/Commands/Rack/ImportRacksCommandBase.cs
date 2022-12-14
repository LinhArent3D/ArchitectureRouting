using System ;
using System.Collections.Generic ;
using System.ComponentModel ;
using System.IO ;
using System.Linq ;
using Arent3d.Architecture.Routing.AppBase ;
using Arent3d.Architecture.Routing.AppBase.Commands ;
using Arent3d.Revit ;
using Arent3d.Revit.Csv ;
using Arent3d.Revit.I18n ;
using Arent3d.Revit.UI ;
using Arent3d.Utility ;
using Autodesk.Revit.Attributes ;
using Autodesk.Revit.DB ;
using Autodesk.Revit.DB.Structure ;
using Autodesk.Revit.UI ;

namespace Arent3d.Architecture.Routing.AppBase.Commands.Rack
{
  public abstract class ImportRacksCommandBase : IExternalCommand
  {
    public Result Execute( ExternalCommandData commandData, ref string message, ElementSet elements )
    {
      var csvFileName = OpenFromToCsv() ;
      if ( null == csvFileName ) return Result.Cancelled ;

      var list = ReadRackRecordsFromFile( csvFileName ).EnumerateAll() ;
      if ( 0 == list.Count ) return Result.Succeeded ;

      var document = commandData.Application.ActiveUIDocument.Document ;
      try {
        var result = document.Transaction( "TransactionName.Commands.Rack.Import".GetAppStringByKeyOrDefault( "Import Pipe Spaces" ), _ =>
        {
          foreach ( var rackRecord in list ) {
            GenerateRack( document, rackRecord ) ;
          }
          return Result.Succeeded ;
        } ) ;

        return result ;
      }
      catch ( Exception e ) {
        CommandUtils.DebugAlertException( e ) ;
        return Result.Failed ;
      }
    }

    private static void GenerateRack( Document document, RackRecord rackRecord )
    {
      var symbol = document.GetFamilySymbols( RoutingFamilyType.RackGuide ).FirstOrDefault() ?? throw new InvalidOperationException() ;
      var instance = symbol.Instantiate( rackRecord.Origin, rackRecord.Level, StructuralType.NonStructural ) ;

      instance.get_Parameter( BuiltInParameter.INSTANCE_ELEVATION_PARAM ).Set( 0.0 ) ;

      rackRecord.Size_X.To( instance, "Revit.Property.Builtin.Width".GetDocumentStringByKeyOrDefault( document, null ) ) ; // TODO
      rackRecord.Size_Y.To( instance, "Revit.Property.Builtin.Height".GetDocumentStringByKeyOrDefault( document, null ) ) ; // TODO
      rackRecord.Size_Z.To( instance, "Revit.Property.Builtin.Length".GetDocumentStringByKeyOrDefault( document, null ) ) ; // TODO
      rackRecord.Offset.To( instance, "Arent-Offset" ) ;
      rackRecord.Elevation.To( instance, BuiltInParameter.INSTANCE_ELEVATION_PARAM ) ;

      ElementTransformUtils.RotateElement( document, instance.Id, Line.CreateBound( XYZ.Zero, XYZ.BasisZ ), rackRecord.RotationDegree.Deg2Rad() ) ;
      ElementTransformUtils.MoveElement( document, instance.Id, rackRecord.Origin - instance.GetTotalTransform().Origin ) ;
    }

    private static IEnumerable<RackRecord> ReadRackRecordsFromFile( string csvFileName )
    {
      using var reader = new StreamReader( csvFileName, true ) ;
      // Cannot use return directly, because `reader` will be closed in that case.
      foreach ( var item in reader.ReadCsvFile<RackRecord>() ) {
        yield return item ;
      }
    }

    private static string? OpenFromToCsv()
    {
      using var dlg = new FileOpenDialog( $"{"Dialog.Commands.Rack.PS.FileName".GetAppStringByKeyOrDefault( "Pipe space list file" )} (*.csv)|*.csv" )
      {
        Title = "Dialog.Commands.Rack.PS.Title.Import".GetAppStringByKeyOrDefault( null )
      } ;

      if ( ItemSelectionDialogResult.Confirmed != dlg.Show() ) return null ;

      return ModelPathUtils.ConvertModelPathToUserVisiblePath( dlg.GetSelectedModelPath() ) ;
    }
  }
}