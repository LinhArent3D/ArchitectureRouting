﻿using System ;
using System.Collections.Generic ;
using System.Diagnostics ;
using System.Linq ;
using System.Windows ;
using System.Windows.Controls ;
using System.Windows.Forms ;
using System.Windows.Input ;
using Arent3d.Architecture.Routing.AppBase.Forms.ValueConverters ;
using Arent3d.Architecture.Routing.AppBase.ViewModel ;
using Arent3d.Architecture.Routing.Extensions ;
using Arent3d.Architecture.Routing.Storable ;
using Arent3d.Architecture.Routing.Storable.Model ;
using Arent3d.Revit ;
using Autodesk.Revit.DB ;
using Autodesk.Revit.UI ;
using DataGridCell = System.Windows.Controls.DataGridCell ;
using KeyEventArgs = System.Windows.Input.KeyEventArgs ;
using MessageBox = System.Windows.MessageBox ;

namespace Arent3d.Architecture.Routing.AppBase.Forms
{
  public partial class RegistrationOfBoardDataDialog
  {
    private readonly Document _document ;
    private RegistrationOfBoardDataViewModel? _allRegistrationOfBoardDataModels ;
    private string _autoControlPanelSearch ;
    private string _signalDestinationSearch ;
    public string SelectedAutoControlPanel { get ; private set ; }
    public string SelectedSignalDestination { get ; private set ; }

    public bool IsFromPowerConnector { get ; private set ; }

    public RegistrationOfBoardDataDialog( UIApplication uiApplication )
    {
      InitializeComponent() ;
      _document = uiApplication.ActiveUIDocument.Document ;
      _allRegistrationOfBoardDataModels = null ;
      _autoControlPanelSearch = string.Empty ;
      _signalDestinationSearch = string.Empty ;
      SelectedAutoControlPanel = string.Empty ;
      SelectedSignalDestination = string.Empty ;

      var oldCeedStorable = _document.GetAllStorables<RegistrationOfBoardDataStorable>().FirstOrDefault() ;
      if ( oldCeedStorable != null ) {
        LoadData( oldCeedStorable ) ;
      }

      Style cellStyle = new( typeof( DataGridCell ) ) ;
      cellStyle.Setters.Add( new EventSetter( MouseDoubleClickEvent, new MouseButtonEventHandler( Cell_DoubleClick ) ) ) ;
      DtGrid.CellStyle = cellStyle ;
    }

    private void Button_LoadData( object sender, RoutedEventArgs e )
    {
      MessageBox.Show( "Please select 盤間配線確認表 file.", "Message" ) ;
      OpenFileDialog openFileDialog = new() { Filter = "Csv files (*.xlsx; *.xls)|*.xlsx;*.xls", Multiselect = false } ;
      string filePath = string.Empty ;

      if ( openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ) {
        filePath = openFileDialog.FileName ;
      }

      if ( string.IsNullOrEmpty( filePath ) ) return ;

      RegistrationOfBoardDataStorable registrationOfBoardDataStorable = _document.GetRegistrationOfBoardDataStorable() ;
      {
        List<RegistrationOfBoardDataModel> registrationOfBoardDataModelData = ExcelToModelConverter.GetAllRegistrationOfBoardDataModel( filePath ) ;
        if ( ! registrationOfBoardDataModelData.Any() ) return ;
        registrationOfBoardDataStorable.RegistrationOfBoardData = registrationOfBoardDataModelData ;

        LoadData( registrationOfBoardDataStorable ) ;
      }
    }

    private void LoadData( RegistrationOfBoardDataStorable registrationOfBoardDataStorable )
    {
      var viewModel = new RegistrationOfBoardDataViewModel( registrationOfBoardDataStorable ) ;
      DataContext = viewModel ;
      _allRegistrationOfBoardDataModels = viewModel ;
      DtGrid.ItemsSource = viewModel.RegistrationOfBoardDataModels ;
      CmbAutoControlPanel.ItemsSource = viewModel.AutoControlPanels ;
      CmbSignalDestination.ItemsSource = viewModel.SignalDestinations ;
    }

    private void LoadData( RegistrationOfBoardDataViewModel registrationOfBoardDataViewModel )
    {
      DataContext = registrationOfBoardDataViewModel ;
      DtGrid.ItemsSource = registrationOfBoardDataViewModel.RegistrationOfBoardDataModels ;
      CmbAutoControlPanel.ItemsSource = registrationOfBoardDataViewModel.AutoControlPanels ;
      CmbSignalDestination.ItemsSource = registrationOfBoardDataViewModel.SignalDestinations ;
    }

    private void CmbAutoControlPanel_TextChanged( object sender, TextChangedEventArgs e )
    {
      _autoControlPanelSearch = ! string.IsNullOrEmpty( CmbAutoControlPanel.Text ) ? CmbAutoControlPanel.Text : string.Empty ;
    }

    private void CmbAutoControlPanel_KeyDown( object sender, KeyEventArgs e )
    {
      if ( e.Key == Key.Enter ) {
        SearchRegistrationOfBoardDataModels() ;
      }
    }

    private void CmbSignalDestination_TextChanged( object sender, TextChangedEventArgs e )
    {
      _signalDestinationSearch = ! string.IsNullOrEmpty( CmbSignalDestination.Text ) ? CmbSignalDestination.Text : string.Empty ;
    }

    private void CmbSignalDestination_KeyDown( object sender, KeyEventArgs e )
    {
      if ( e.Key == Key.Enter ) {
        SearchRegistrationOfBoardDataModels() ;
      }
    }

    private void Button_Search( object sender, RoutedEventArgs e )
    {
      SearchRegistrationOfBoardDataModels() ;
    }

    private void SearchRegistrationOfBoardDataModels()
    {
      if ( _allRegistrationOfBoardDataModels == null ) return ;
      if ( string.IsNullOrEmpty( _autoControlPanelSearch ) && string.IsNullOrEmpty( _signalDestinationSearch ) ) {
        DtGrid.ItemsSource = _allRegistrationOfBoardDataModels.RegistrationOfBoardDataModels ;
      }
      else {
        var registrationOfBoardDataModels = new List<RegistrationOfBoardDataModel>() ;
        if ( ! string.IsNullOrEmpty( _autoControlPanelSearch ) && ! string.IsNullOrEmpty( _signalDestinationSearch ) )
          registrationOfBoardDataModels = _allRegistrationOfBoardDataModels.RegistrationOfBoardDataModels.Where( c => c.AutoControlPanel.Contains( _autoControlPanelSearch ) && c.SignalDestination.Contains( _signalDestinationSearch ) ).ToList() ;
        else if ( ! string.IsNullOrEmpty( _autoControlPanelSearch ) && string.IsNullOrEmpty( _signalDestinationSearch ) )
          registrationOfBoardDataModels = _allRegistrationOfBoardDataModels.RegistrationOfBoardDataModels.Where( c => c.AutoControlPanel.Contains( _autoControlPanelSearch ) ).ToList() ;
        else if ( string.IsNullOrEmpty( _autoControlPanelSearch ) && ! string.IsNullOrEmpty( _signalDestinationSearch ) )
          registrationOfBoardDataModels = _allRegistrationOfBoardDataModels.RegistrationOfBoardDataModels.Where( c => c.SignalDestination.Contains( _signalDestinationSearch ) ).ToList() ;

        DtGrid.ItemsSource = registrationOfBoardDataModels ;
      }
    }

    private void Button_Reset( object sender, RoutedEventArgs e )
    {
      CmbAutoControlPanel.SelectedIndex = -1 ;
      CmbAutoControlPanel.Text = String.Empty ;
      CmbSignalDestination.SelectedIndex = -1 ;
      CmbSignalDestination.Text = String.Empty ;
      if ( _allRegistrationOfBoardDataModels != null )
        LoadData( _allRegistrationOfBoardDataModels ) ;
    }

    private void Button_OK( object sender, RoutedEventArgs e )
    {
      SaveCeedModelNumberDisplayAndOnlyUsingCodeState() ;
      DialogResult = true ;
      Close() ;
    }

    private void SaveCeedModelNumberDisplayAndOnlyUsingCodeState()
    {
      if ( _allRegistrationOfBoardDataModels == null ) return ;
      var registrationOfBoardDataStorable = _document.GetRegistrationOfBoardDataStorable() ;
      try {
        using Transaction t = new( _document, "Save data" ) ;
        t.Start() ;
        registrationOfBoardDataStorable.RegistrationOfBoardData = _allRegistrationOfBoardDataModels.RegistrationOfBoardDataModels ;
        registrationOfBoardDataStorable.Save() ;
        t.Commit() ;
      }
      catch ( Autodesk.Revit.Exceptions.OperationCanceledException ) {
      }
    }

    private void Cell_DoubleClick( object sender, MouseButtonEventArgs e )
    {
      var dataGridCellTarget = (DataGridCell) sender ;
      var dataContext = dataGridCellTarget.DataContext as RegistrationOfBoardDataModel ;
      var textBlock = dataGridCellTarget.Content as TextBlock ;
      var cellValue = textBlock?.Text ;
      var column = dataGridCellTarget.Column as DataGridColumn ;
      var columnIndex = column?.DisplayIndex ;

      if ( string.IsNullOrEmpty( cellValue ) ) return ;
      SelectedAutoControlPanel = dataContext?.AutoControlPanel ?? string.Empty ;
      SelectedSignalDestination = dataContext?.SignalDestination ?? string.Empty ;
      if ( columnIndex == 0 ) IsFromPowerConnector = true ;
      if ( columnIndex == 1 ) IsFromPowerConnector = false ;
      SaveCeedModelNumberDisplayAndOnlyUsingCodeState() ;
      DialogResult = true ;
      Close() ;
    }
  }
}