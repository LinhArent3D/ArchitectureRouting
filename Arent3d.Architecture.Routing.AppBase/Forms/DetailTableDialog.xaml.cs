﻿using System ;
using System.Collections.Generic ;
using System.Collections.ObjectModel ;
using System.Linq ;
using System.Windows ;
using System.Windows.Controls ;
using Arent3d.Architecture.Routing.AppBase.Commands.Initialization ;
using Arent3d.Architecture.Routing.AppBase.ViewModel ;
using Arent3d.Architecture.Routing.Storable.Model ;

namespace Arent3d.Architecture.Routing.AppBase.Forms
{
  public partial class DetailTableDialog : Window
  {
    private readonly List<ConduitsModel> _conduitsModelData ;
    private readonly DetailTableViewModel _detailTableViewModel ;
    public readonly Dictionary<string, string> RoutesChangedConstructionItem ;

    public DetailTableDialog( DetailTableViewModel viewModel, List<ConduitsModel> conduitsModelData )
    {
      InitializeComponent() ;
      DataContext = viewModel ;
      _detailTableViewModel = viewModel ;
      _conduitsModelData = conduitsModelData ;
      RoutesChangedConstructionItem = new Dictionary<string, string>() ;
    }

    private void BtnSave_OnClick( object sender, RoutedEventArgs e )
    {
      DialogResult = true ;
      this.Close() ;
    }

    private void BtnCompleted_OnClick( object sender, RoutedEventArgs e )
    {
      DialogResult = true ;
      this.Close() ;
    }

    private void PlumpingTypeSelectionChanged( object sender, SelectionChangedEventArgs e )
    {
      if ( sender is not ComboBox comboBox ) return ;
      var plumbingType = comboBox.SelectedValue ;
      if ( plumbingType == null ) return ;
      if ( DtGrid.SelectedItem is not DetailTableModel detailTableModel || detailTableModel.PlumbingType == plumbingType!.ToString() ) return ;
      if ( plumbingType!.ToString() == "↑" ) {
        comboBox.SelectedValue = detailTableModel.PlumbingType ;
      }
      else {
        List<DetailTableModel> detailTableModels = _detailTableViewModel.DetailTableModels.Where( c => c.DetailSymbolId == detailTableModel.DetailSymbolId ).ToList() ;

        List<DetailTableModel> newDetailTableModels = detailTableModels.Select( x => x ).ToList() ;

        CreateDetailTableCommandBase.SetPlumbingDataForOneSymbol( _conduitsModelData, ref newDetailTableModels, plumbingType!.ToString() ) ;


        foreach ( var oldDetailTableModel in detailTableModels ) {
          _detailTableViewModel.DetailTableModels.Remove( oldDetailTableModel ) ;
        }

        foreach ( var newDetailSymbolModel in newDetailTableModels ) {
          _detailTableViewModel.DetailTableModels.Add( newDetailSymbolModel ) ;
        }

        newDetailTableModels = _detailTableViewModel.DetailTableModels.OrderBy( x => x.DetailSymbol ).ThenByDescending( x => x.DetailSymbolId ).ThenByDescending( x => x.IsParentRoute ).GroupBy( x => x.DetailSymbolId ).SelectMany( x => x ).ToList() ;
        _detailTableViewModel.DetailTableModels = new ObservableCollection<DetailTableModel>( newDetailTableModels ) ;
        this.DataContext = _detailTableViewModel ;
        DtGrid.ItemsSource = _detailTableViewModel.DetailTableModels ;
      }
    }

    private void ConstructionItemSelectionChanged( object sender, SelectionChangedEventArgs e )
    {
      if ( sender is not ComboBox comboBox ) return ;
      var constructionItem = comboBox.SelectedValue ;
      if ( constructionItem == null ) return ;
      if ( DtGrid.SelectedItem is not DetailTableModel detailTableModel || detailTableModel.ConstructionItems == constructionItem!.ToString() ) return ;
      var detailTableModelsSameRoute = _detailTableViewModel.DetailTableModels.Where( c => c.RouteName == detailTableModel.RouteName ).ToList() ;
      List<DetailTableModel> newDetailTableModels = new List<DetailTableModel>() ;
      foreach ( var oldDetailTableModel in _detailTableViewModel.DetailTableModels ) {
        if ( detailTableModelsSameRoute.Contains( oldDetailTableModel ) ) {
          var newDetailSymbolModel = new DetailTableModel( oldDetailTableModel.CalculationExclusion, oldDetailTableModel.Floor, oldDetailTableModel.CeeDCode, oldDetailTableModel.DetailSymbol, oldDetailTableModel.DetailSymbolId, oldDetailTableModel.WireType, oldDetailTableModel.WireSize, oldDetailTableModel.WireStrip, oldDetailTableModel.WireBook, oldDetailTableModel.EarthType, oldDetailTableModel.EarthSize, oldDetailTableModel.NumberOfGrounds, oldDetailTableModel.PlumbingType, oldDetailTableModel.PlumbingSize, oldDetailTableModel.NumberOfPlumbing, oldDetailTableModel.ConstructionClassification, oldDetailTableModel.SignalType, constructionItem!.ToString(), constructionItem!.ToString(), oldDetailTableModel.Remark, oldDetailTableModel.WireCrossSectionalArea, oldDetailTableModel.CountCableSamePosition, oldDetailTableModel.RouteName, oldDetailTableModel.IsEcoMode, oldDetailTableModel.IsParentRoute, oldDetailTableModel.IsReadOnly ) ;
          newDetailTableModels.Add( newDetailSymbolModel ) ;
        }
        else {
          newDetailTableModels.Add( oldDetailTableModel ) ;
        }
      }

      if ( ! RoutesChangedConstructionItem.ContainsKey( detailTableModel.RouteName! ) ) {
        RoutesChangedConstructionItem.Add( detailTableModel.RouteName!, constructionItem!.ToString() ) ;
      }
      else {
        RoutesChangedConstructionItem[ detailTableModel.RouteName! ] = constructionItem!.ToString() ;
      }

      _detailTableViewModel.DetailTableModels = new ObservableCollection<DetailTableModel>( newDetailTableModels ) ;
      this.DataContext = _detailTableViewModel ;
      DtGrid.ItemsSource = _detailTableViewModel.DetailTableModels ;
    }
  }
}