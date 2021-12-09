﻿using System ;
using System.Collections.Generic ;
using System.Linq ;
using System.Windows.Forms ;
using Arent3d.Architecture.Routing.AppBase.Forms ;
using Arent3d.Architecture.Routing.AppBase.Selection ;
using Arent3d.Architecture.Routing.Extensions ;
using Arent3d.Architecture.Routing.Storable ;
using Arent3d.Architecture.Routing.Storable.Model ;
using Arent3d.Revit ;
using Arent3d.Revit.I18n ;
using Arent3d.Revit.UI ;
using Autodesk.Revit.DB ;
using Autodesk.Revit.DB.Electrical ;
using Autodesk.Revit.UI ;
using Autodesk.Revit.UI.Selection ;
using Application = Autodesk.Revit.ApplicationServices.Application ;

namespace Arent3d.Architecture.Routing.AppBase.Commands.Initialization
{
  public class CreateDetailSymbolCommandBase : IExternalCommand
  {
    public Result Execute( ExternalCommandData commandData, ref string message, ElementSet elements )
    {
      var doc = commandData.Application.ActiveUIDocument.Document ;
      var activeView = doc.ActiveView ;
      if ( activeView is View3D ) {
        const string mess = "Please select cable on view 2D." ;
        MessageBox.Show( mess, "Message" ) ;
        return Result.Cancelled ;
      }
      
      var uiDoc = commandData.Application.ActiveUIDocument ;
      var selection = uiDoc.Selection ;
      UIApplication uiApp = commandData.Application ;
      Application app = uiApp.Application ;
      var isLeft = true ;
      var detailSymbolStorable = doc.GetAllStorables<DetailSymbolStorable>().FirstOrDefault() ?? doc.GetDetailSymbolStorable() ;
      List<string> conduitHasSymbolId = detailSymbolStorable.DetailSymbolModelData.Select( d => d.ConduitId ).ToList() ;

      return doc.Transaction( "TransactionName.Commands.Routing.AddSymbol".GetAppStringByKeyOrDefault( "Create Detail Symbol" ), _ =>
      {
        var element = selection.PickObject( ObjectType.Element, ConduitSelectionFilter.Instance, "Select cable." ) ;
        var conduit = doc.GetElement( element.ElementId ) ;
        if ( conduitHasSymbolId.Contains( conduit.Id.IntegerValue.ToString() ) ) {
          const string mess = "Conduit has been added the detail symbol." ;
          MessageBox.Show( mess, "Message" ) ;
          return Result.Cancelled ;
        }

        var (symbols, angle) = CreateValueForCombobox( doc, detailSymbolStorable.DetailSymbolModelData, conduit ) ;
        var detailSymbolSettingDialog = new DetailSymbolSettingDialog( symbols, angle ) ;
        detailSymbolSettingDialog.ShowDialog() ;
        if ( ! ( detailSymbolSettingDialog.DialogResult ?? false ) ) return Result.Cancelled ;

        const double dPlus = 0.2 ;
        CurveArray lines = app.Create.NewCurveArray() ;
        XYZ firstPoint = element.GlobalPoint ;
        var startLineP1 = new XYZ( firstPoint.X + dPlus, firstPoint.Y + dPlus, firstPoint.Z ) ;
        var endLineP1 = new XYZ( firstPoint.X - dPlus, firstPoint.Y - dPlus, firstPoint.Z ) ;
        Curve startCurve = Line.CreateBound( startLineP1, endLineP1 ) ;
        var startLine = doc.Create.NewDetailCurve( doc.ActiveView, startCurve ) ;
        var subCategory = GetLineStyle( doc ) ;
        startLine.LineStyle = subCategory.GetGraphicsStyle( GraphicsStyleType.Projection ) ;

        List<XYZ> points = new List<XYZ>() ;
        switch ( detailSymbolSettingDialog.Angle ) {
          case "0" :
            points.Add( new XYZ( firstPoint.X - dPlus * 10, firstPoint.Y, firstPoint.Z ) ) ;
            firstPoint = new XYZ( firstPoint.X + dPlus, firstPoint.Y, firstPoint.Z ) ;
            isLeft = true ;
            break ;
          case "90" :
            points.Add( new XYZ( firstPoint.X, firstPoint.Y + dPlus * 10, firstPoint.Z ) ) ;
            points.Add( new XYZ( firstPoint.X - dPlus * 7, firstPoint.Y + dPlus * 10, firstPoint.Z ) ) ;
            firstPoint = new XYZ( firstPoint.X, firstPoint.Y - dPlus, firstPoint.Z ) ;
            isLeft = true ;
            break ;
          case "180" :
            points.Add( new XYZ( firstPoint.X + dPlus * 10, firstPoint.Y, firstPoint.Z ) ) ;
            firstPoint = new XYZ( firstPoint.X - dPlus, firstPoint.Y, firstPoint.Z ) ;
            isLeft = false ;
            break ;
          case "-90" :
            points.Add( new XYZ( firstPoint.X, firstPoint.Y - dPlus * 10, firstPoint.Z ) ) ;
            points.Add( new XYZ( firstPoint.X + dPlus * 7, firstPoint.Y - dPlus * 10, firstPoint.Z ) ) ;
            firstPoint = new XYZ( firstPoint.X, firstPoint.Y + dPlus, firstPoint.Z ) ;
            isLeft = false ;
            break ;
        }

        foreach ( var nextP in points ) {
          if ( firstPoint.DistanceTo( nextP ) > 0.001 ) {
            var curve = Line.CreateBound( firstPoint, nextP ) ;
            lines.Append( curve ) ;
          }

          firstPoint = nextP ;
        }

        doc.Create.NewDetailCurveArray( doc.ActiveView, lines ) ;

        ElementId defaultTextTypeId = doc.GetDefaultElementTypeId( ElementTypeGroup.TextNoteType ) ;
        var noteWidth = detailSymbolSettingDialog.HeightCharacter * detailSymbolSettingDialog.PercentWidth ;

        // make sure note width works for the text type
        var minWidth = TextElement.GetMinimumAllowedWidth( doc, defaultTextTypeId ) ;
        var maxWidth = TextElement.GetMaximumAllowedWidth( doc, defaultTextTypeId ) ;
        if ( noteWidth < minWidth ) {
          noteWidth = minWidth ;
        }
        else if ( noteWidth > maxWidth ) {
          noteWidth = maxWidth ;
        }

        TextNoteOptions opts = new( defaultTextTypeId ) { HorizontalAlignment = HorizontalTextAlignment.Left } ;

        var txtPosition = new XYZ( firstPoint.X + ( isLeft ? dPlus : -dPlus * 5 ), firstPoint.Y + dPlus * 6.5, firstPoint.Z ) ;
        TextNote.Create( doc, doc.ActiveView.Id, txtPosition, noteWidth, detailSymbolSettingDialog.DetailSymbol, opts ) ;

        SaveDetailSymbol( doc, detailSymbolStorable, conduit, detailSymbolSettingDialog.DetailSymbol ) ;
        return Result.Succeeded ;
      } ) ;
    }

    private void SaveDetailSymbol( Document doc, DetailSymbolStorable detailSymbolStorable, Element conduit, string detailSymbol )
    {
      try {
        detailSymbolStorable.DetailSymbolModelData.AddRange( AddDetailSymbol( doc, conduit, detailSymbol ) ) ;
        detailSymbolStorable.Save() ;
      }
      catch ( Autodesk.Revit.Exceptions.OperationCanceledException ) {
        MessageBox.Show( "Save Data Failed.", "Error Message" ) ;
      }
    }

    private List<DetailSymbolModel> AddDetailSymbol( Document doc, Element conduit, string detailSymbol )
    {
      List<DetailSymbolModel> detailSymbolModels = new List<DetailSymbolModel>() ;
      List<Element> allConnector = doc.GetAllElements<Element>().OfCategory( BuiltInCategorySets.PickUpElements ).ToList() ;
      List<Element> allConduit = doc.GetAllElements<Element>().OfCategory( BuiltInCategorySets.Conduits ).Where( c => c.Id != conduit.Id ).ToList() ;
      DetailSymbolModel detailSymbolModel = CreateDetailSymbolModel( doc, allConnector, conduit, detailSymbol ) ;
      detailSymbolModels.Add( detailSymbolModel ) ;
      AddDetailSymbolForConduitSameFromToConnectors( doc, allConduit, allConnector, detailSymbolModels, detailSymbol, detailSymbolModel.FromConnectorId, detailSymbolModel.ToConnectorId ) ;
      return detailSymbolModels ;
    }

    private DetailSymbolModel CreateDetailSymbolModel( Document doc, List<Element> allConnectors, Element conduit, string detailSymbol )
    {
      var code = string.Empty ;
      var fromElementId = string.Empty ;
      var toElementId = string.Empty ;

      var fromEndPoint = conduit.GetNearestEndPoints( true ).ToList() ;
      if ( fromEndPoint.Any() ) {
        var fromEndPointKey = fromEndPoint.FirstOrDefault()?.Key ;
        fromElementId = fromEndPointKey!.GetElementId() ;
        if ( ! string.IsNullOrEmpty( fromElementId ) ) {
          var fromConnector = allConnectors.FirstOrDefault( c => c.Id.IntegerValue.ToString() == fromElementId ) ;
          if ( fromConnector!.IsTerminatePoint() || fromConnector!.IsPassPoint() ) {
            fromConnector!.TryGetProperty( PassPointParameter.RelatedConnectorId, out string? fromConnectorId ) ;
            if ( ! string.IsNullOrEmpty( fromConnectorId ) ) {
              fromElementId = fromConnectorId! ;
            }
          }
        }
      }

      var toEndPoint = conduit.GetNearestEndPoints( false ).ToList() ;
      if ( toEndPoint.Any() ) {
        var toEndPointKey = toEndPoint.FirstOrDefault()?.Key ;
        toElementId = toEndPointKey!.GetElementId() ;
        if ( ! string.IsNullOrEmpty( toElementId ) ) {
          var toConnector = allConnectors.FirstOrDefault( c => c.Id.IntegerValue.ToString() == toElementId ) ;
          if ( toConnector!.IsTerminatePoint() || toConnector!.IsPassPoint() ) {
            toConnector!.TryGetProperty( PassPointParameter.RelatedConnectorId, out string? toConnectorId ) ;
            toElementId = toConnectorId! ;
            if ( ! string.IsNullOrEmpty( toElementId ) )
              toConnector = allConnectors.FirstOrDefault( c => c.Id.IntegerValue.ToString() == toElementId ) ;
          }

          if ( toConnector != null )
            code = GetCeeDSetCodeOfElement( doc, toConnector ) ;
        }
      }

      DetailSymbolModel detailSymbolModel = new DetailSymbolModel( detailSymbol, conduit.Id.IntegerValue.ToString(), fromElementId, toElementId, code ) ;
      return detailSymbolModel ;
    }

    private string GetCeeDSetCodeOfElement( Document doc, Element element )
    {
      var ceeDSetCode = string.Empty ;
      if ( element.GroupId == ElementId.InvalidElementId ) return ceeDSetCode ?? string.Empty ;
      var groupId = doc.GetAllElements<Group>().FirstOrDefault( g => g.AttachedParentId == element.GroupId )?.Id ;
      if ( groupId != null )
        ceeDSetCode = doc.GetAllElements<TextNote>().FirstOrDefault( t => t.GroupId == groupId )?.Text.Trim( '\r' ) ;

      return ceeDSetCode ?? string.Empty ;
    }

    private Category GetLineStyle( Document doc )
    {
      var categories = doc.Settings.Categories ;
      var subCategoryName = "MySubCategory" ;
      Category category = doc.Settings.Categories.get_Item( BuiltInCategory.OST_GenericAnnotation ) ;
      Category subCategory ;
      if ( ! category.SubCategories.Contains( subCategoryName ) ) {
        subCategory = categories.NewSubcategory( category, subCategoryName ) ;
        var newColor = new Color( 0, 250, 0 ) ;
        subCategory.LineColor = newColor ;
      }
      else
        subCategory = category.SubCategories.get_Item( subCategoryName ) ;

      return subCategory ;
    }

    private void AddDetailSymbolForConduitSameFromToConnectors( Document doc, List<Element> allConduit, List<Element> allConnector, List<DetailSymbolModel> detailSymbolModels, string detailSymbol, string fromConnectorId, string toConnectorId )
    {
      foreach ( var conduit in allConduit ) {
        DetailSymbolModel detailSymbolModel = CreateDetailSymbolModel( doc, allConnector, conduit, detailSymbol ) ;
        if ( detailSymbolModel.FromConnectorId == fromConnectorId && detailSymbolModel.ToConnectorId == toConnectorId )
          detailSymbolModels.Add( detailSymbolModel ) ;
      }
    }

    private (List<string>, List<int>) CreateValueForCombobox( Document doc, List<DetailSymbolModel> detailSymbolModels, Element conduit )
    {
      List<string> symbols = new List<string>() ;
      if ( detailSymbolModels.Any() ) {
        var symbol = CheckDetailSymbolOfConduitSameCode( doc, conduit, detailSymbolModels ) ;
        if ( ! string.IsNullOrEmpty( symbol ) ) {
          symbols.Add( symbol ) ;
        }
        else {
          List<string> detailSymbols = detailSymbolModels.Select( d => d.DetailSymbol ).ToList() ;
          for ( var letter = 'A' ; letter <= 'Z' ; letter++ ) {
            if ( detailSymbols.Contains( letter.ToString() ) ) continue ;
            symbols.Add( letter.ToString() ) ;
          }
        }
      }
      else {
        for ( var letter = 'A' ; letter <= 'Z' ; letter++ ) {
          symbols.Add( letter.ToString() ) ;
        }
      }

      List<int> angle = new List<int>() ;
      if ( conduit is Conduit ) {
        var location = ( conduit.Location as LocationCurve )! ;
        var line = ( location.Curve as Line )! ;
        if ( line.Direction.X is 1.0 or -1.0 )
          angle.AddRange( new List<int>() { 90, -90 } ) ;
        else if ( line.Direction.Y is 1.0 or -1.0 )
          angle.AddRange( new List<int>() { 0, 180 } ) ;
        else {
          angle.AddRange( new List<int>() { 0, 90, 180, -90 } ) ;
        }
      }
      else {
        var elbow = ( conduit as FamilyInstance )! ;
        if ( elbow.FacingOrientation.X is 1.0 or -1.0 )
          angle.AddRange( new List<int>() { 90, -90 } ) ;
        else if ( elbow.FacingOrientation.Y is 1.0 or -1.0 )
          angle.AddRange( new List<int>() { 0, 180 } ) ;
        else {
          angle.AddRange( new List<int>() { 0, 90, 180, -90 } ) ;
        }
      }

      return ( symbols, angle ) ;
    }

    private string CheckDetailSymbolOfConduitSameCode( Document doc, Element conduit, List<DetailSymbolModel> detailSymbolModels )
    {
      var detailSymbol = string.Empty ;
      List<Element> allConnectors = doc.GetAllElements<Element>().OfCategory( BuiltInCategorySets.PickUpElements ).ToList() ;
      var toEndPoint = conduit.GetNearestEndPoints( false ).ToList() ;
      if ( ! toEndPoint.Any() ) return detailSymbol ;
      var toEndPointKey = toEndPoint.FirstOrDefault()?.Key ;
      var toElementId = toEndPointKey!.GetElementId() ;

      if ( string.IsNullOrEmpty( toElementId ) ) return detailSymbol ;
      var toConnector = allConnectors.FirstOrDefault( c => c.Id.IntegerValue.ToString() == toElementId ) ;
      if ( toConnector!.IsTerminatePoint() || toConnector!.IsPassPoint() ) {
        toConnector!.TryGetProperty( PassPointParameter.RelatedConnectorId, out string? toConnectorId ) ;
        toElementId = toConnectorId! ;
        if ( ! string.IsNullOrEmpty( toElementId ) )
          toConnector = allConnectors.FirstOrDefault( c => c.Id.IntegerValue.ToString() == toElementId ) ;
      }

      if ( toConnector == null ) return detailSymbol ;
      var code = GetCeeDSetCodeOfElement( doc, toConnector ) ;
      detailSymbol = detailSymbolModels.Where( d => d.Code == code ).Select( d => d.DetailSymbol ).FirstOrDefault() ?? string.Empty ;
      return detailSymbol ;
    }
  }
}