﻿using System ;
using System.Collections.Generic ;
using System.Collections.ObjectModel ;
using System.IO ;
using System.Linq ;
using System.Reflection ;
using System.Text ;
using System.Windows ;
using System.Windows.Forms ;
using System.Windows.Media ;
using Arent3d.Architecture.Routing.AppBase.Commands.Routing ;
using Arent3d.Architecture.Routing.AppBase.Commands.Shaft ;
using Arent3d.Architecture.Routing.AppBase.Manager ;
using Arent3d.Architecture.Routing.Extensions ;
using Arent3d.Architecture.Routing.Storable.Model ;
using Arent3d.Revit.I18n ;
using Autodesk.Revit.DB ;
using Color = Autodesk.Revit.DB.Color ;
using MessageBox = System.Windows.MessageBox ;

namespace Arent3d.Architecture.Routing.AppBase.ViewModel
{
  public class LayerNameSettingViewModel : NotifyPropertyChanged
  {
    private readonly List<Layer> _newLayerNames ;
    private readonly List<Layer> _oldLayerNames ;
    private readonly Document _document ;
    private readonly string _settingFilePath ;

    public ObservableCollection<Layer> Layers { get ; }
    public List<AutoCadColorsManager.AutoCadColor> AutoCadColors { get ; }

    public string LayerNames { get; set ; }

    public RelayCommand<Window> ExportFileDwgCommand => new(ExportDwg) ;

    public LayerNameSettingViewModel( Document document )
    {
      _document = document ;
      _settingFilePath = GetSettingPath() ;
      _newLayerNames = new List<Layer>() ;
      _oldLayerNames = new List<Layer>() ;
      AutoCadColors = AutoCadColorsManager.GetAutoCadColorDict() ;
      Layers = new ObservableCollection<Layer>() ;
      LayerNames = string.Empty ;
      var layers = GetLayerNames( _settingFilePath ) ;
      if ( ! layers.Any() ) return ;
      SetDataSource( layers, AutoCadColors ) ;
    }

    private void SetDataSource( List<Layer> layers, List<AutoCadColorsManager.AutoCadColor> autoCadColors )
    {
      Layers.Clear() ;
      foreach ( var layer in layers ) {
        if ( string.IsNullOrEmpty( layer.Index ) ) {
          layer.Index = AutoCadColorsManager.NoColor ;
          layer.SolidColor = new SolidColorBrush() ;
        }
        else {
          var solidColor = autoCadColors.FirstOrDefault( c => c.Index == layer.Index )?.SolidColor ?? new SolidColorBrush() ;
          layer.SolidColor = solidColor ;
        }

        Layers.Add( layer ) ;
        _newLayerNames.Add( layer ) ;
        _oldLayerNames.Add( new Layer( layer.LayerName, layer.FamilyName, layer.Index ) ) ;
      }
    }

    private string GetSettingPath()
    {
      string resourcesPath = Path.Combine( Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location )!, "resources" )  ;
      var layerSettingsFileName = "Electrical.App.Commands.Initialization.ExportDWGCommand.ArentExportLayersFile".GetDocumentStringByKeyOrDefault( _document, "Arent-export-layers.txt" ) ;
      var filePath = Path.Combine( resourcesPath, layerSettingsFileName ) ;

      return filePath ;
    }

    private void ExportDwg( Window window )
    {
      var activeView = _document.ActiveView ;
      SaveFileDialog saveFileDialog = new() { Filter = "DWG file (*.dwg)|*.dwg", InitialDirectory = Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments ) } ;
      if ( saveFileDialog.ShowDialog() != DialogResult.OK ) return ;
      var filePath = Path.GetDirectoryName( saveFileDialog.FileName ) ;
      var fileName = Path.GetFileName( saveFileDialog.FileName ) ;
      DWGExportOptions options = new() { LayerMapping = _settingFilePath } ;
      List<ElementId> viewIds = new() { activeView.Id } ;
      // replace text
      var encoding = GetEncoding( _settingFilePath ) ;
      ReplaceLayerNames( _oldLayerNames, _newLayerNames, _settingFilePath, encoding ) ;
      
      // Delete layers
      DeleteLayers(LayerNames) ;

      using var transaction = new Transaction( _document ) ;
      transaction.Start( "Override Element Graphic" ) ;
      var overrideGraphic = new OverrideGraphicSettings() ;
      overrideGraphic.SetProjectionLineColor( new Color( 255, 255, 255 ) ) ;
      var curveElements = _document.GetAllInstances<CurveElement>(_document.ActiveView).Where(x => x.LineStyle.Name == CreateCylindricalShaftCommandBase.SubCategoryForSymbol).ToList() ;
      curveElements.ForEach(x => _document.ActiveView.SetElementOverrides(x.Id, overrideGraphic));
      transaction.Commit() ;
      
      // export dwg
      _document.Export( filePath, fileName, viewIds, options ) ;
      
      transaction.Start( "Reset Element Graphic" ) ;
      var defaultGraphic = new OverrideGraphicSettings() ;
      curveElements.ForEach(x => _document.ActiveView.SetElementOverrides(x.Id, defaultGraphic));
      transaction.Commit() ;

      // close window
      window.DialogResult = true ;
      window.Close() ;
    }
    
    private void DeleteLayers( string stringListLayer )
    {
      var listLayer = stringListLayer.Split( ',' ).Select( p => p.Trim() ).ToList() ;
      var categories = _document.Settings.Categories.Cast<Category>().ToList() ;
      
      foreach ( var category in categories ) {
        List<Category> layers = category.SubCategories.Cast<Category>().Where( x=>listLayer.Contains( x.Name  )).ToList() ;
        if ( layers.Any() ) {
          foreach ( var layer in layers ) {
            _document.Delete( layer.Id ) ;
          }
        }
      }
    }
  


    private static List<Layer> GetLayerNames( string filePath )
    {
      const string exceptString = "Ifc" ;
      var names = new List<Layer>() ;
      using ( var reader = File.OpenText( filePath ) ) {
        while ( reader.ReadLine() is { } line ) {
          if ( line[ 0 ] == '#' ) continue ;
          var words = line.Split( '\t' ) ;
          var familyName = words[ 0 ] ;
          var typeOfLayer = words[ 1 ] ;
          var layerName = words[ 2 ] ;
          var colorIndex = words.Length > 3 ? words[ 3 ] : string.Empty ;
          var familyType = "" ;
          if ( typeOfLayer != "" ) {
            familyType = $" ({typeOfLayer})" ;
          }

          names.Add( new Layer( layerName, familyName + familyType, colorIndex ) ) ;
        }

        reader.Close() ;
      }

      var filterNames = names.Distinct()
        .Where( x => ! x.LayerName.Contains( exceptString ) && ! string.IsNullOrEmpty( x.LayerName ) )
        .GroupBy( x => x.LayerName )
        .Select( ng => new Layer 
        { 
          LayerName = ng.First().LayerName, 
          FamilyName = string.Join( "\n", ng.Select( x => x.FamilyName ).ToArray() ), 
          Index = ng.First().Index 
        } )
        .ToList() ;

      return filterNames ;
    }

    private static Encoding GetEncoding( string filename )
    {
      var bom = new byte[ 4 ] ;
      using ( var file = new FileStream( filename, FileMode.Open, FileAccess.Read ) ) {
        file.Read( bom, 0, 4 ) ;
      }

      if ( bom[ 0 ] == 0x2b && bom[ 1 ] == 0x2f && bom[ 2 ] == 0x76 ) return Encoding.UTF7 ;
      if ( bom[ 0 ] == 0xef && bom[ 1 ] == 0xbb && bom[ 2 ] == 0xbf ) return Encoding.UTF8 ;
      if ( bom[ 0 ] == 0xff && bom[ 1 ] == 0xfe && bom[ 2 ] == 0 && bom[ 3 ] == 0 ) return Encoding.UTF32 ; //UTF-32LE
      if ( bom[ 0 ] == 0xff && bom[ 1 ] == 0xfe ) return Encoding.Unicode ; //UTF-16LE
      if ( bom[ 0 ] == 0xfe && bom[ 1 ] == 0xff ) return Encoding.BigEndianUnicode ; //UTF-16BE
      if ( bom[ 0 ] == 0 && bom[ 1 ] == 0 && bom[ 2 ] == 0xfe && bom[ 3 ] == 0xff ) return new UTF32Encoding( true, true ) ; //UTF-32BE

      return Encoding.ASCII ;
    }

    private static void ReplaceLayerNames( IReadOnlyList<Layer> oldLayerNames, IReadOnlyList<Layer> newLayerNames, string filePath, Encoding encoding )
    {
      try {
        var hasChange = false ;
        var content = File.ReadAllText( filePath ) ;
        for ( var i = 0 ; i < newLayerNames.Count() ; i++ ) {
          if ( oldLayerNames[ i ].LayerName == newLayerNames[ i ].LayerName && oldLayerNames[ i ].Index == newLayerNames[ i ].Index ) continue ;
          hasChange = true ;
          var oldValue = oldLayerNames[ i ].LayerName + ( oldLayerNames[ i ].Index == AutoCadColorsManager.NoColor ? string.Empty : "\t" + oldLayerNames[ i ].Index ) ;
          var newValue = newLayerNames[ i ].LayerName + ( newLayerNames[ i ].Index == AutoCadColorsManager.NoColor ? string.Empty : "\t" + newLayerNames[ i ].Index ) ;
          content = content.Replace( oldValue, newValue ) ;
        }

        if ( hasChange ) File.WriteAllText( filePath, content, encoding ) ;
      }
      catch ( Exception e ) {
        MessageBox.Show( e.Message, "Error" ) ;
      }
    }
  }

  public class Layer
  {
    public string LayerName { get ; set ; }

    public string FamilyName { get ; set ; }
    public string Index { get ; set ; }
    public SolidColorBrush SolidColor { get ; set ; }

    public Layer()
    {
      LayerName = string.Empty ;
      FamilyName = string.Empty ;
      Index = string.Empty ;
      SolidColor = new SolidColorBrush() ;
    }

    public Layer( string layerName, string familyName, string colorIndex )
    {
      LayerName = layerName ;
      FamilyName = familyName ;
      Index = colorIndex ;
      SolidColor = new SolidColorBrush() ;
    }
  }
}