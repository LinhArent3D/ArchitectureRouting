﻿using System.Collections.Generic ;
using Arent3d.Architecture.Routing.Storable.Model ;
using Autodesk.Revit.DB ;
using System ;
using System.Collections.ObjectModel ;
using System.Collections.Specialized ;
using System.IO ;
using System.Linq ;
using System.Windows ;
using System.Windows.Forms ;
using Arent3d.Architecture.Routing.AppBase.Commands.Routing ;
using Arent3d.Architecture.Routing.AppBase.Forms ;
using Arent3d.Architecture.Routing.AppBase.ViewModel ;
using Arent3d.Architecture.Routing.Storable ;
using Arent3d.Revit ;
using Arent3d.Revit.UI ;
using Arent3d.Utility ;
using NPOI.XSSF.UserModel ;
using NPOI.SS.UserModel ;
using NPOI.SS.Util ;
using BorderStyle = NPOI.SS.UserModel.BorderStyle ;
using CheckBox = System.Windows.Controls.CheckBox ;
using MessageBox = System.Windows.Forms.MessageBox ;
using RadioButton = System.Windows.Controls.RadioButton ;
using Arent3d.Architecture.Routing.Extensions ;
using MoreLinq ;
using MoreLinq.Extensions ;


namespace Arent3d.Architecture.Routing.AppBase.ViewModel
{
  public class PickUpReportViewModel : NotifyPropertyChanged
  {
    private const string SummaryFileType = "拾い出し集計表" ;
    private const string ConfirmationFileType = "拾い根拠確認表" ;
    private string DoconOff => FileName + "OFF" ;
    private string DoconOn => FileName + "ON" ;
    private const string OnText = "ON" ;
    private const string OffText = "OFF" ;
    private const string OutputItemAll = "全項目出力" ;
    private const string OutputItemSelection = "出力項目選択" ;
    private const string SummaryFileName = "_拾い出し集計表.xlsx" ;
    private const string ConfirmationFileName = "_拾い根拠確認表.xlsx" ;
    private const string DefaultConstructionItem = "未設定" ;
    private const string LengthItem = "長さ物" ;
    private const string ConstructionMaterialItem = "工事部材" ;
    private const string EquipmentMountingItem = "機器取付" ;
    private const string WiringItem = "結線" ;
    private const string BoardItem  = "盤搬入据付" ;
    private const string InteriorRepairEquipmentItem = "内装・補修・設備" ;
    private const string OtherItem = "その他" ;
    
    private readonly List<HiroiMasterModel> _hiroiMasterModels ;
    private readonly Document _document ;
    
    public ObservableCollection<PickUpModel> PickUpModels { get ; set ; }
    public ObservableCollection<ListBoxItem> FileTypes { get ; set ; }
    public ObservableCollection<ListBoxItem> DoconTypes { get ; set ; }
    public ObservableCollection<ListBoxItem> OutputItems { get ; set ; }
    
    public ObservableCollection<ListBoxItem> CurrentSettingList { get ; set ; }
    public ObservableCollection<ListBoxItem> PreviousSettingList { get ; set ; }

    private string _pathName ;

    public string PathName
    {
      get => _pathName ;
      set
      {
        _pathName = value ;
        OnPropertyChanged();
      }
    }

    private List<string> _fileNames ;
    
    private string _fileName ;

    public string FileName
    {
      get => _fileName ;
      set
      {
        _fileName = value ;
        OnPropertyChanged();
      }
    }

    private bool _isOutputItemsEnable ;

    public bool IsOutputItemsEnable
    {
      get => _isOutputItemsEnable;
      set
      {
        _isOutputItemsEnable = value ;
        OnPropertyChanged() ;
      }
    }

    public RelayCommand GetSaveLocationCommand => new( GetSaveLocation ) ;
    public RelayCommand<Window> CancelCommand => new( Cancel ) ;
    public RelayCommand<Window> ExecuteCommand => new( Execute ) ;
    public RelayCommand SettingCommand => new( OutputItemsSelectionSetting ) ;
    public RelayCommand<Window> SetOptionCommand => new( SetOption ) ;
    
    
    public PickUpReportViewModel( Document document )
    {
      _document = document ;
      PickUpModels = new ObservableCollection<PickUpModel>() ;
      FileTypes = new ObservableCollection<ListBoxItem>() ;
      DoconTypes = new ObservableCollection<ListBoxItem>() ;
      OutputItems = new ObservableCollection<ListBoxItem>() ;
      CurrentSettingList = new ObservableCollection<ListBoxItem>() ;
      PreviousSettingList = new ObservableCollection<ListBoxItem>() ;
      _hiroiMasterModels = new List<HiroiMasterModel>() ;
      
      var csvStorable = _document.GetAllStorables<CsvStorable>().FirstOrDefault() ;
      if ( csvStorable != null ) _hiroiMasterModels = csvStorable.HiroiMasterModelData ;

      _pathName = string.Empty ;
      _fileName = string.Empty ;
      _fileNames = new List<string>() ;
      CreateCheckBoxList() ;
      InitPickUpModels() ;
    }

    private void InitPickUpModels()
    {
      var pickUpStorable = _document.GetAllStorables<PickUpStorable>().FirstOrDefault() ;
      if ( pickUpStorable != null ) PickUpModels = new ObservableCollection<PickUpModel>( pickUpStorable.AllPickUpModelData ) ;
    }

    private void GetSaveLocation()
    {
      const string fileName = "フォルダを選択してください.xlsx" ;
      SaveFileDialog saveFileDialog = new SaveFileDialog { FileName = fileName, InitialDirectory = Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments ) } ;

      if ( saveFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK ) return ;
      PathName = Path.GetDirectoryName( saveFileDialog.FileName )! ;
    }

    private void Execute( Window window )
    {
      if ( _fileNames.Any() && ! string.IsNullOrEmpty( PathName ) && ! string.IsNullOrEmpty( FileName ) ) {
        CreateOutputFile() ;
        window.DialogResult = true ;
        window.Close() ;
      }
      else {
        if ( ! _fileNames.Any() && string.IsNullOrEmpty( PathName ) )
          MessageBox.Show( "Please select the output folder and file type.", "Warning" ) ;
        else if ( string.IsNullOrEmpty( PathName ) )
          MessageBox.Show( "Please select the output folder.", "Warning" ) ;
        else if ( ! _fileNames.Any() )
          MessageBox.Show( "Please select the output file type.", "Warning" ) ;
        else if (  string.IsNullOrEmpty( FileName ) )
          MessageBox.Show( "出力ファイル名を入力してください。" ) ;
      }
    }
    
    private void Cancel( Window window)
    {
      window.DialogResult = false ;
      window.Close() ;
    }
    
    private void CreateCheckBoxList()
    {
      // FileTypes
      FileTypes.Add( new ListBoxItem { TheText = SummaryFileType, TheValue = false } ) ;
      FileTypes.Add( new ListBoxItem { TheText = ConfirmationFileType, TheValue = false } ) ;

      // DoconTypes
      DoconTypes.Add( new ListBoxItem { TheText = OnText, TheValue = true } ) ;
      DoconTypes.Add( new ListBoxItem { TheText = OffText, TheValue = false } ) ;
      
      // OutputItems
      OutputItems.Add( new ListBoxItem { TheText = OutputItemAll, TheValue = true } );
      OutputItems.Add( new ListBoxItem { TheText = OutputItemSelection, TheValue = false } );

      //SettingList
      CreateSettingList() ;
    }

    private void CreateSettingList()
    {
      CurrentSettingList.Add( new ListBoxItem { TheText = LengthItem, TheValue = true } );
      CurrentSettingList.Add( new ListBoxItem { TheText = ConstructionMaterialItem, TheValue = true } );
      CurrentSettingList.Add( new ListBoxItem { TheText = EquipmentMountingItem, TheValue = false } );
      CurrentSettingList.Add( new ListBoxItem { TheText = WiringItem, TheValue = false } );
      CurrentSettingList.Add( new ListBoxItem { TheText = BoardItem, TheValue = false } );
      CurrentSettingList.Add( new ListBoxItem { TheText = InteriorRepairEquipmentItem, TheValue = true } );
      CurrentSettingList.Add( new ListBoxItem { TheText = OtherItem, TheValue = false } );

      PreviousSettingList = new ObservableCollection<ListBoxItem>( CurrentSettingList.Select( x => x.Copy() ).ToList() ) ;
    }
    
    public void OutputItemsChecked( object sender )
    {
      var radioButton = sender as RadioButton ;
      IsOutputItemsEnable = radioButton?.Content.ToString() == OutputItemSelection ;
    }

    public void DoconItemChecked( object sender )
    {
      _fileNames = new List<string>() ;
      var radioButton = sender as RadioButton ;
      var fileTypes = FileTypes.Where( f => f.TheValue == true ).Select( f => f.TheText ).ToList() ;
      var docon = radioButton!.Content.ToString() == OnText ? DoconOn : DoconOff ;
      foreach ( var fileType in fileTypes ) {
        string fileName = string.Empty ;
        switch ( fileType ) {
          case SummaryFileType :
            fileName = SummaryFileName ;
            break ;
          case ConfirmationFileType :
            fileName = ConfirmationFileName ;
            break ;
        }

        if ( string.IsNullOrEmpty( fileName ) ) continue ;
        _fileNames.Add( docon + fileName ) ;
      }
      
    }

    public void FileTypeChecked( object sender )
    {
      var checkbox = sender as CheckBox ;
      var docon = DoconTypes.First().TheValue ? DoconOn : DoconOff ;
      switch ( checkbox!.Content.ToString() ) {
        case SummaryFileType :
          if ( ! _fileNames.Contains( docon + SummaryFileName ) )
            _fileNames.Add( docon + SummaryFileName ) ;
          break ;
        case ConfirmationFileType :
          if ( ! _fileNames.Contains( docon + ConfirmationFileName ) )
            _fileNames.Add( docon + ConfirmationFileName ) ;
          break ;
      }
      
    }

    public void FileTypeUnchecked( object sender )
    {
      var checkbox = sender as CheckBox ;
      var docon = DoconTypes.First().TheValue ? DoconOn : DoconOff ;
      switch ( checkbox!.Content.ToString() ) {
        case SummaryFileType :
          if ( _fileNames.Contains( docon + SummaryFileName ) )
            _fileNames.Remove( docon + SummaryFileName ) ;
          break ;
        case ConfirmationFileType :
          if ( _fileNames.Contains( docon + ConfirmationFileName ) )
            _fileNames.Remove( docon + ConfirmationFileName ) ;
          break ;
      }
      
    }

    private string GetFileName(string fileName)
    {
      return string.IsNullOrEmpty( _fileName ) ? fileName : $"{_fileName}_{fileName}" ;
    }

    private List<string> GetConstructionItemList()
    {
      var constructionItemList = new List<string>() ;
      foreach ( var pickUpModel in PickUpModels.Where( pickUpModel =>
                 ! constructionItemList.Contains( pickUpModel.ConstructionItems ) && pickUpModel.EquipmentType == PickUpViewModel.ProductType.Conduit.GetFieldName() ) ) {
        constructionItemList.Add( pickUpModel.ConstructionItems ) ;
      }

      return constructionItemList ;
    }

    private void CreateOutputFile()
    {
      GetPickModels() ;
      if ( ! PickUpModels.Any() ) MessageBox.Show( "Don't have pick up data.", "Message" ) ;
      try {
        var constructionItemList = GetConstructionItemList() ;
        if ( ! constructionItemList.Any() ) constructionItemList.Add( DefaultConstructionItem ) ;
        foreach ( var fileName in _fileNames ) {
          XSSFWorkbook workbook = new XSSFWorkbook() ;

          Dictionary<string, XSSFCellStyle> xssfCellStyles = new Dictionary<string, XSSFCellStyle>
          {
            { "borderedCellStyle", CreateCellStyle( workbook, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Thin, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Center ) },
            { "noneBorderedCellStyle", CreateCellStyle( workbook, BorderStyle.None, BorderStyle.None, BorderStyle.None, BorderStyle.None, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Left ) },
            { "bottomBorderedCellStyle", CreateCellStyle( workbook, BorderStyle.None, BorderStyle.None, BorderStyle.None, BorderStyle.Thin, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Left ) },
            { "leftBottomBorderedCellStyle", CreateCellStyle( workbook, BorderStyle.Thin, BorderStyle.None, BorderStyle.None, BorderStyle.Thin, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Left ) },
            { "rightBottomBorderedCellStyle", CreateCellStyle( workbook, BorderStyle.None, BorderStyle.Thin, BorderStyle.None, BorderStyle.Thin, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Right ) },
            { "leftAlignmentLeftRightBorderedCellStyle", CreateCellStyle( workbook, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.None, BorderStyle.None, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Left ) },
            { "leftRightBorderedCellStyle", CreateCellStyle( workbook, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.None, BorderStyle.None, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Center ) },
            { "exceptTopBorderedCellStyle", CreateCellStyle( workbook, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.None, BorderStyle.Thin, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Left ) },
            { "wrapTextBorderedCellStyle", CreateCellStyle( workbook, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Thin, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Left, true ) },
            { "borderedCellStyleMedium", CreateCellStyle( workbook, BorderStyle.Medium, BorderStyle.Medium, BorderStyle.Medium, BorderStyle.Medium, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Center ) },
            { "bottomBorderedCellStyleMedium", CreateCellStyle( workbook, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.None, BorderStyle.Medium, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Left ) },
            { "topBorderedCellStyleMedium", CreateCellStyle( workbook, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Medium, BorderStyle.Thin, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Center ) },
            { "leftBottomBorderedCellStyleMedium", CreateCellStyle( workbook, BorderStyle.Medium, BorderStyle.None, BorderStyle.None, BorderStyle.Thin, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Left ) },
            { "rightBottomBorderedCellStyleMedium", CreateCellStyle( workbook, BorderStyle.None, BorderStyle.Medium, BorderStyle.None, BorderStyle.Thin, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Center ) },
            { "leftAlignmentLeftRightBorderedCellStyleMedium", CreateCellStyle( workbook, BorderStyle.Medium, BorderStyle.Medium, BorderStyle.None, BorderStyle.None, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Left ) },
            { "leftRightBorderedCellStyleMedium", CreateCellStyle( workbook, BorderStyle.Medium, BorderStyle.Thin, BorderStyle.None, BorderStyle.None, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Center ) },
            { "exceptTopBorderedCellStyleMedium", CreateCellStyle( workbook, BorderStyle.Medium, BorderStyle.Medium, BorderStyle.None, BorderStyle.Medium, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Left ) },
            { "wrapTextBorderedCellStyleMedium", CreateCellStyle( workbook, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Thin, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Left, true ) },
            { "leftRightBottomBorderedCellStyleMedium", CreateCellStyle( workbook, BorderStyle.Medium, BorderStyle.Medium, BorderStyle.None, BorderStyle.Medium, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Right ) },
            { "leftRightBottomBorderedCellStyleMediumThin", CreateCellStyle( workbook, BorderStyle.Medium, BorderStyle.Medium, BorderStyle.None, BorderStyle.Thin, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Right ) },
            { "leftRightTopBorderedCellStyleMedium", CreateCellStyle( workbook, BorderStyle.Medium, BorderStyle.Medium, BorderStyle.Medium, BorderStyle.None, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Center ) },
            { "topRightBorderedCellStyleMedium", CreateCellStyle( workbook, BorderStyle.Medium, BorderStyle.Medium, BorderStyle.Medium, BorderStyle.None, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Center ) },
            { "leftTopBorderedCellStyleMedium", CreateCellStyle( workbook, BorderStyle.Medium, BorderStyle.None, BorderStyle.Medium, BorderStyle.None, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Center ) },
            { "rightBorderedCellStyleMedium", CreateCellStyle( workbook, BorderStyle.None, BorderStyle.Medium, BorderStyle.None, BorderStyle.Thin, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Center ) },
            { "rightBorderedCellStyleMediumDotted", CreateCellStyle( workbook, BorderStyle.None, BorderStyle.Medium, BorderStyle.None, BorderStyle.None, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Center ) },
            { "leftBorderedCellStyleMedium", CreateCellStyle( workbook, BorderStyle.Medium, BorderStyle.Thin, BorderStyle.None, BorderStyle.None, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Left ) },
            { "exceptTopBorderedCellStyleSummary", CreateCellStyle( workbook, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Dotted, BorderStyle.Thin, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Left ) },
            { "exceptTopBorderedCellStyleSummaryMedium", CreateCellStyle( workbook, BorderStyle.Thin, BorderStyle.Medium, BorderStyle.Dotted, BorderStyle.Thin, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Left ) },
            { "bottomCellStyleSummaryMedium", CreateCellStyle( workbook, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Dotted, BorderStyle.Medium, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Left ) },
            { "rightBottomCellStyleSummaryMedium", CreateCellStyle( workbook, BorderStyle.Thin, BorderStyle.Medium, BorderStyle.Dotted, BorderStyle.Medium, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Left ) },
            { "leftBottomBorderedCellStyleLastRow", CreateCellStyle( workbook, BorderStyle.Medium, BorderStyle.None, BorderStyle.None, BorderStyle.Medium, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Left ) },
            { "bottomBorderedCellStyleLastRow", CreateCellStyle( workbook, BorderStyle.None, BorderStyle.None, BorderStyle.None, BorderStyle.Medium, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Left ) },
            { "rightBottomBorderedCellStyleLastRow", CreateCellStyle( workbook, BorderStyle.Thin, BorderStyle.Medium, BorderStyle.None, BorderStyle.Medium, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Right ) }
          } ;
          var headerNoneBorderedCellStyle = CreateCellStyle( workbook, BorderStyle.None, BorderStyle.None, BorderStyle.None, BorderStyle.None, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Left ) ;
          XSSFFont myFont = (XSSFFont) workbook.CreateFont() ;
          myFont.FontHeightInPoints = 18 ;
          myFont.IsBold = true ;
          myFont.FontName = "ＭＳ ゴシック";
          headerNoneBorderedCellStyle.SetFont( myFont ) ;
          xssfCellStyles.Add( "headerNoneBorderedCellStyle", headerNoneBorderedCellStyle ) ;

          if ( fileName.Contains( SummaryFileName ) )
            foreach ( var sheetName in constructionItemList ) {
              CreateSheet( SheetType.Summary, workbook, sheetName, xssfCellStyles ) ;
            }
          else if ( fileName.Contains( ConfirmationFileName ) )
            foreach ( var sheetName in constructionItemList ) {
              CreateSheet( SheetType.Confirmation, workbook, sheetName, xssfCellStyles ) ;
            }

          var fileNameToOutPut = GetFileName(fileName) ;
          FileStream fs = new FileStream( PathName + @"\" + fileNameToOutPut, FileMode.OpenOrCreate ) ;
          workbook.Write( fs ) ;

          workbook.Close() ;
          fs.Close() ;
        }

        MessageBox.Show( "Export pick-up output file successfully.", "Message" ) ;
      }
      catch ( Exception ex ) {
        MessageBox.Show( "Export file failed because " + ex, "Error message" ) ;
      }
    }

    private enum SheetType
    {
      Confirmation,
      Summary
    }

    private void CreateSheet( SheetType sheetType, IWorkbook workbook, string sheetName, IReadOnlyDictionary<string, XSSFCellStyle> xssfCellStyles )
    {
      List<string> levels = _document.GetAllElements<Level>().Select( l => l.Name ).ToList() ;
      var codeList = GetCodeList() ;
      var docon = DoconTypes.First().TheValue ? DoconOn : DoconOff ;

      ISheet sheet = workbook.CreateSheet( sheetName ) ;
      IRow row0, row2 ;
      int rowStart ;
      switch ( sheetType ) {
        case SheetType.Confirmation :
          sheet.SetColumnWidth( 0, 500 ) ;
          sheet.SetColumnWidth( 1, 8000 ) ;
          sheet.SetColumnWidth( 2, 8000 ) ;
          sheet.SetColumnWidth( 3, 4000 ) ;
          sheet.SetColumnWidth( 4, 1200 ) ;
          sheet.SetColumnWidth( 5, 4000 ) ;
          sheet.SetColumnWidth( 7, 3000 ) ;
          sheet.SetColumnWidth( 16, 2500 ) ;
          rowStart = 0 ;
          foreach ( var level in levels ) {
            if( PickUpModels.All( x => x.Floor != level ) ) continue;
            row0 = sheet.CreateRow( rowStart ) ;
            var row1 = sheet.CreateRow( rowStart + 1 ) ;
            row2 = sheet.CreateRow( rowStart + 2 ) ;
            CreateMergeCell( sheet, row0, rowStart, rowStart, 2, 6, docon, xssfCellStyles[ "bottomBorderedCellStyle" ] ) ;
            CreateCell( row0, 13, "縮尺 :", xssfCellStyles[ "bottomBorderedCellStyle" ] ) ;
            CreateCell( row0, 14, "", xssfCellStyles[ "bottomBorderedCellStyle" ] ) ;
            CreateCell( row0, 15, "階高 :", xssfCellStyles[ "bottomBorderedCellStyle" ] ) ;
            CreateCell( row0, 16, "", xssfCellStyles[ "bottomBorderedCellStyle" ] ) ;

            CreateCell( row1, 1, "【入力確認表】", xssfCellStyles[ "headerNoneBorderedCellStyle" ] ) ;
            CreateCell( row1, 2, "工事階層 :", xssfCellStyles[ "noneBorderedCellStyle" ] ) ;
            CreateMergeCell( sheet, row1, rowStart + 1, rowStart + 1, 3, 6, sheetName, xssfCellStyles[ "noneBorderedCellStyle" ] ) ;
            CreateCell( row1, 7, "図面番号 :", xssfCellStyles[ "noneBorderedCellStyle" ] ) ;
            CreateMergeCell( sheet, row1, rowStart + 1, rowStart + 1, 8, 9, level, xssfCellStyles[ "noneBorderedCellStyle" ] ) ;
            CreateCell( row1, 10, "階数 :", xssfCellStyles[ "noneBorderedCellStyle" ] ) ;
            CreateCell( row1, 12, "区間 :", xssfCellStyles[ "noneBorderedCellStyle" ] ) ;
            CreateMergeCell( sheet, row1, rowStart + 1, rowStart + 1, 13, 16, "", xssfCellStyles[ "noneBorderedCellStyle" ] ) ;

            CreateCell( row2, 1, "品名", xssfCellStyles[ "borderedCellStyleMedium" ] ) ;
            CreateMergeCell( sheet, row2, rowStart + 2, rowStart + 2, 2, 3, "規格", xssfCellStyles[ "borderedCellStyleMedium" ], true ) ;
            CreateCell( row2, 4, "単位", xssfCellStyles[ "borderedCellStyleMedium" ] ) ;
            CreateMergeCell( sheet, row2, rowStart + 2, rowStart + 2, 5, 15, "軌跡", xssfCellStyles[ "borderedCellStyleMedium" ], true ) ;
            CreateCell( row2, 16, "合計数量", xssfCellStyles[ "borderedCellStyleMedium" ] ) ;

            rowStart += 3 ;
            List<KeyValuePair<string, List<PickUpModel>>> dictionaryDataPickUpModel = new List<KeyValuePair<string, List<PickUpModel>>>() ;
            
            foreach ( var code in codeList ) {
              var dataPickUpModels = PickUpModels
                .Where( p => p.ConstructionItems == sheetName && p.Specification2 == code && p.Floor == level )
                .GroupBy( x => x.ProductCode, ( key, p ) => new { ProductCode = key, PickUpModels = p.ToList() } ) ;
            
              foreach ( var dataPickUpModel in dataPickUpModels ) {
                if ( dictionaryDataPickUpModel.Any( l => l.Key == dataPickUpModel.ProductCode ) && ! IsTani( dataPickUpModel.PickUpModels.First() ) ) {
                  var dataPickUpModelExist = dictionaryDataPickUpModel.Single( x => x.Key == dataPickUpModel.ProductCode ) ;
                  dataPickUpModelExist.Value.AddRange( dataPickUpModel.PickUpModels );
                }
                else {
                  dictionaryDataPickUpModel.Add( new KeyValuePair<string, List<PickUpModel>>(dataPickUpModel.ProductCode, dataPickUpModel.PickUpModels) );
                }
              }
            }

            var dictionaryDataPickUpModelOrder = dictionaryDataPickUpModel.OrderBy( x => x.Value.First().Tani == "m" ? 1 : 2).ThenBy( c => c.Value.First().ProductName ).ThenBy( c => c.Value.First().Standard ) ; ;
            foreach ( var dataPickUpModel in dictionaryDataPickUpModelOrder ) {
              rowStart = AddConfirmationPickUpRow( dataPickUpModel.Value, sheet, rowStart, xssfCellStyles ) ;
            }

            var lastRow = sheet.CreateRow( rowStart ) ;
            CreateCell( lastRow, 1, "", xssfCellStyles[ "leftRightBottomBorderedCellStyleMedium" ] ) ;
            CreateMergeCell( sheet, lastRow, rowStart, rowStart, 2, 3, "", xssfCellStyles[ "leftRightBottomBorderedCellStyleMedium" ], true ) ;
            CreateCell( lastRow, 4, "", xssfCellStyles[ "leftRightBottomBorderedCellStyleMedium" ] ) ;
            CreateMergeCell( sheet, lastRow, rowStart, rowStart, 5, 15, "", xssfCellStyles[ "leftRightBottomBorderedCellStyleMedium" ], true ) ;
            CreateCell( lastRow, 16, "", xssfCellStyles[ "leftRightBottomBorderedCellStyleMedium" ] ) ;

            rowStart += 2 ;
          }

          break ;
        case SheetType.Summary :
          sheet.SetColumnWidth( 0, 500 ) ;
          sheet.SetColumnWidth( 1, 500 ) ;
          sheet.SetColumnWidth( 2, 8000 ) ;
          sheet.SetColumnWidth( 4, 1200 ) ;
          row0 = sheet.CreateRow( 0 ) ;
          row2 = sheet.CreateRow( 2 ) ;
          var row3 = sheet.CreateRow( 3 ) ;
          CreateCell( row0, 2, "【拾い出し集計表】", xssfCellStyles[ "headerNoneBorderedCellStyle" ] ) ;
          CreateMergeCell( sheet, row0, 0, 0, 6, 7, docon, xssfCellStyles[ "bottomBorderedCellStyle" ] ) ;
          for ( var i = 7 ; i < 19 ; i++ ) {
            CreateCell( row0, i, "", xssfCellStyles[ "bottomBorderedCellStyle" ] ) ;
          }

          CreateCell( row0, 14, sheetName, xssfCellStyles[ "bottomBorderedCellStyle" ] ) ;

          CreateMergeCell( sheet, row2, 2, 2, 1, 3, "", CreateCellStyle( workbook, BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Medium, BorderStyle.None, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Center ), true ) ;
          CreateCell( row2, 4, "", CreateCellStyle( workbook, BorderStyle.None, BorderStyle.Medium, BorderStyle.Medium, BorderStyle.Thin, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Center ) ) ;
          Dictionary<int, string> levelColumns = new Dictionary<int, string>() ;
          var index = 5 ;
          foreach ( var level in levels ) {
            CreateCell( row2, index, level, xssfCellStyles[ "topBorderedCellStyleMedium" ] ) ;
            levelColumns.Add( index, level ) ;
            CreateCell( row3, index, "", xssfCellStyles[ "bottomBorderedCellStyleMedium" ] ) ;
            index++ ;
          }

          CreateCell( row2, index, "合計", CreateCellStyle( workbook, BorderStyle.Thin, BorderStyle.Medium, BorderStyle.Medium, BorderStyle.Thin, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Center ) ) ;

          CreateMergeCell( sheet, row3, 3, 3, 1, 3, "品名/規格", CreateCellStyle( workbook, BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Medium, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Center ), true ) ;
          CreateCell( row3, 4, "単位", CreateCellStyle( workbook, BorderStyle.Thin, BorderStyle.Medium, BorderStyle.None, BorderStyle.Medium, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Center ) ) ;
          CreateCell( row3, index, "", CreateCellStyle( workbook, BorderStyle.None, BorderStyle.Medium, BorderStyle.None, BorderStyle.Medium, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Center ) ) ;

          rowStart = 4 ;
          List<KeyValuePair<string, List<PickUpModel>>> dictionaryDataPickUpModelSummary = new List<KeyValuePair<string, List<PickUpModel>>>() ;
          foreach ( var code in codeList ) {
            var dataPickUpModels = PickUpModels
              .Where( p => p.ConstructionItems == sheetName && p.Specification2 == code )
              .GroupBy( x => x.ProductCode, ( key, p ) => new { ProductCode = key, PickUpModels = p.ToList() } ) ;
            foreach ( var dataPickUpModel in dataPickUpModels ) {
              if ( dictionaryDataPickUpModelSummary.Any( l => l.Key == dataPickUpModel.ProductCode ) && ! IsTani( dataPickUpModel.PickUpModels.First() ) ) {
                var dataPickUpModelExist = dictionaryDataPickUpModelSummary.Single( x => x.Key == dataPickUpModel.ProductCode ) ;
                dataPickUpModelExist.Value.AddRange( dataPickUpModel.PickUpModels );
              }
              else {
                dictionaryDataPickUpModelSummary.Add( new KeyValuePair<string, List<PickUpModel>>(dataPickUpModel.ProductCode, dataPickUpModel.PickUpModels) );
              }
            }
          }
          
          var dictionaryDataPickUpModelOrderSummary = dictionaryDataPickUpModelSummary.OrderBy( x => x.Value.First().Tani == "m" ? 1 : 2).ThenBy( c => c.Value.First().ProductName ).ThenBy( c => c.Value.First().Standard ).ToList() ;
          foreach ( var dataPickUpModel in dictionaryDataPickUpModelOrderSummary ) {
            if ( rowStart + 2 == (dictionaryDataPickUpModelOrderSummary.Count * 2 + 4)) {
              rowStart = AddSummaryPickUpRow( dataPickUpModel.Value, sheet, rowStart, levelColumns, index, xssfCellStyles, true ) ;
            }
            else {
              rowStart = AddSummaryPickUpRow( dataPickUpModel.Value, sheet, rowStart, levelColumns, index, xssfCellStyles ) ;
            }
          }
          
          break ;
      }
    }

    private List<string> GetCodeList()
    {
      var codeList = new List<string>() ;
      foreach ( var pickUpModel in PickUpModels.Where( pickUpModel => ! codeList.Contains( pickUpModel.Specification2 ) ) ) {
        codeList.Add( pickUpModel.Specification2 ) ;
      }

      return codeList ;
    }

    private int AddSummaryPickUpRow( 
      List<PickUpModel> pickUpModels, 
      ISheet sheet, 
      int rowStart, 
      IReadOnlyDictionary<int, string> levelColumns, 
      int index,
      IReadOnlyDictionary<string, XSSFCellStyle> xssfCellStyles,
      bool isLastRow = false
      )
    {
      if ( ! pickUpModels.Any() ) return rowStart ;
      var pickUpModel = pickUpModels.First() ;
      var rowName = sheet.CreateRow( rowStart ) ;
      var isTani = IsTani( pickUpModel ) ;
      CreateMergeCell( sheet, rowName, rowStart, rowStart, 1, 3, pickUpModel.ProductName, xssfCellStyles[ "leftBorderedCellStyleMedium" ], true ) ;
      CreateMergeCell( sheet, rowName, rowStart, rowStart + 1, 4, 4, pickUpModel.Tani, xssfCellStyles[ "rightBorderedCellStyleMedium" ], true ) ;

      rowStart++ ;
      var rowStandard = sheet.CreateRow( rowStart ) ;
      if ( isLastRow ) {
        CreateCell( rowStandard, 1, "", xssfCellStyles[ "leftBottomBorderedCellStyleLastRow" ] ) ;
        CreateCell( rowStandard, 2, pickUpModel.Standard, xssfCellStyles[ "bottomBorderedCellStyleLastRow" ] ) ;
        CreateCell( rowStandard, 3, "", xssfCellStyles[ "bottomBorderedCellStyleLastRow" ] ) ;
        CreateCell( rowStandard, 4, "", xssfCellStyles[ "rightBottomBorderedCellStyleLastRow" ] ) ;
      }
      else {
        CreateCell( rowStandard, 1, "", xssfCellStyles[ "leftBottomBorderedCellStyleMedium" ] ) ;
        CreateCell( rowStandard, 2, pickUpModel.Standard, xssfCellStyles[ "bottomBorderedCellStyle" ] ) ;
        CreateCell( rowStandard, 3, "", xssfCellStyles[ "rightBottomBorderedCellStyle" ] ) ;
        CreateCell( rowStandard, 4, "", xssfCellStyles[ "rightBottomBorderedCellStyleMedium" ] ) ;
      }
      
      double total = 0 ;
      for ( var i = 5 ; i < index ; i++ ) {
        double quantityFloor = 0 ;
        var level = levelColumns[ i ] ;
        foreach ( var item in pickUpModels.Where( item => item.Floor == level ) ) {
          double.TryParse( item.Quantity, out var quantity ) ;
          quantityFloor += quantity ;
        }

        CreateCell( rowName, i, quantityFloor == 0 ? string.Empty : Math.Round( quantityFloor, isTani ? 1 : 2 ).ToString(), xssfCellStyles[ "leftRightBorderedCellStyle" ] ) ;
        CreateCell( rowStandard, i, "", isLastRow ? xssfCellStyles[ "bottomCellStyleSummaryMedium" ] : xssfCellStyles[ "exceptTopBorderedCellStyleSummary" ] ) ;


        total += quantityFloor ;
      }

      CreateCell( rowName, index, total == 0 ? string.Empty : Math.Round( total, isTani ? 1 : 2 ).ToString(), xssfCellStyles[ "rightBorderedCellStyleMediumDotted" ] ) ;
      CreateCell( rowStandard, index, "", isLastRow ? xssfCellStyles[ "rightBottomCellStyleSummaryMedium" ] : xssfCellStyles[ "exceptTopBorderedCellStyleSummaryMedium" ] ) ;

      rowStart++ ;
      return rowStart ;
    }

    private List<string> GetPickUpNumbersList( List<PickUpModel> pickUpModels )
    {
      var pickUpNumberList = new List<string>() ;
      foreach ( var pickUpModel in pickUpModels.Where( pickUpModel => ! pickUpNumberList.Contains( pickUpModel.PickUpNumber ) ) ) {
        pickUpNumberList.Add( pickUpModel.PickUpNumber ) ;
      }

      return pickUpNumberList ;
    }

    private int AddConfirmationPickUpRow( List<PickUpModel> pickUpModels, ISheet sheet, int rowStart, IReadOnlyDictionary<string, XSSFCellStyle> xssfCellStyles )
    {
      if ( ! pickUpModels.Any() ) return rowStart ;
      var pickUpNumbers = GetPickUpNumbersList( pickUpModels ) ;
      var pickUpModel = pickUpModels.First() ;
      var row = sheet.CreateRow( rowStart ) ;
      var isTani = IsTani( pickUpModel ) ;
      CreateCell( row, 1, pickUpModel.ProductName, xssfCellStyles[ "leftBottomBorderedCellStyleMedium" ] ) ;
      CreateCell( row, 2, pickUpModel.Standard, xssfCellStyles[ "leftBottomBorderedCellStyleMedium" ] ) ;
      CreateCell( row, 3, "", xssfCellStyles[ "rightBottomBorderedCellStyleMedium" ] ) ;
      CreateCell( row, 4, pickUpModel.Tani, xssfCellStyles[ "rightBottomBorderedCellStyleMedium" ] ) ;
      
      double total = 0 ;
      Dictionary<string, int> trajectory = new Dictionary<string, int>() ;
      foreach ( var pickUpNumber in pickUpNumbers ) {
        double seenQuantity = 0 ;
        string stringNotTani = string.Empty ;
        Dictionary<string, double> notSeenQuantities = new Dictionary<string, double>() ;
        var items = pickUpModels.Where( p => p.PickUpNumber == pickUpNumber ).ToList() ;
        foreach ( var item in items.Where( item => ! string.IsNullOrEmpty( item.Quantity ) ) ) {
          double.TryParse( item.Quantity, out var quantity ) ;
          if ( ! string.IsNullOrEmpty( item.Direction ) ) {
            if ( ! notSeenQuantities.Keys.Contains( item.Direction ) ) {
              notSeenQuantities.Add( item.Direction, 0 ) ;
            }

            notSeenQuantities[ item.Direction ] += quantity ;
          }
          else {
            if ( ! isTani ) stringNotTani += string.IsNullOrEmpty( stringNotTani ) ? item.SumQuantity : $"+{item.SumQuantity}" ;
            seenQuantity += quantity ;
          }
          
          total += quantity ;
        }
        
        var number = DoconTypes.First().TheValue && !string.IsNullOrEmpty(pickUpNumber) ? "[" + pickUpNumber + "]" : string.Empty ;
        var seenQuantityStr = isTani ? ( seenQuantity > 0 ? Math.Round( seenQuantity, isTani ? 1 : 2 ).ToString() : string.Empty ) : stringNotTani ;
        var notSeenQuantityStr = string.Empty ;
        foreach ( var (_, value) in notSeenQuantities ) {
          notSeenQuantityStr += value > 0 ? " + ↓" + Math.Round( value, isTani ? 1 : 2 ) : string.Empty ;
        }

        var key = isTani ? ( "( " + seenQuantityStr + notSeenQuantityStr + " )" ) : ( seenQuantityStr + notSeenQuantityStr ) ;
        var itemKey = trajectory.FirstOrDefault( t => t.Key.Contains( key ) ).Key ;
        if ( string.IsNullOrEmpty( itemKey ) )
          trajectory.Add( number + key, 1 ) ;
        else {
          trajectory[ itemKey ]++ ;
        }
      }

      List<string> trajectoryStr = ( from item in trajectory select item.Value == 1 ? item.Key : item.Key + " x " + item.Value ).ToList() ;
      int firstCellIndex = 5 ;
      int lastCellIndex = 15 ;
      float lengthOfCellMerge = GetWidthOfCellMerge( sheet, 5, 15 ) ;

      var valueOfCell = string.Empty ;
      var trajectoryStrCount = trajectoryStr.Count ;
      var count = 0 ;
      if ( trajectoryStrCount > 3 ) {
        for ( var i = 0 ; i < trajectoryStrCount ; i++ ) {
          valueOfCell += trajectoryStr[ i ] + " + ";
          if ( valueOfCell.Length * 1.5  < lengthOfCellMerge/256.0  ) continue;
          if ( count == 0 ) {
            CreateMergeCell( sheet, row, rowStart, rowStart, 5, 15, string.Join( " + ", valueOfCell ), xssfCellStyles[ "leftBottomBorderedCellStyleMedium" ] ) ;
            count++ ;
          }
          else {
            var rowTrajectory = sheet.CreateRow( ++rowStart ) ;
            CreateCell( rowTrajectory, 1, "", xssfCellStyles[ "leftBottomBorderedCellStyleMedium" ] ) ;
            CreateCell( rowTrajectory, 2, "", xssfCellStyles[ "leftBottomBorderedCellStyleMedium" ] ) ;
            CreateCell( rowTrajectory, 3, "", xssfCellStyles[ "rightBottomBorderedCellStyleMedium" ] ) ;
            CreateCell( rowTrajectory, 4, "", xssfCellStyles[ "rightBottomBorderedCellStyleMedium" ] ) ;
            CreateCell( rowTrajectory, 16, "", xssfCellStyles[ "leftRightBottomBorderedCellStyleMediumThin" ] ) ;
            CreateMergeCell( sheet, rowTrajectory, rowStart, rowStart, 5, 15, string.Join( " + ", valueOfCell ), xssfCellStyles[ "leftBottomBorderedCellStyleMedium" ] ) ;
          }

          valueOfCell = string.Empty ;
        }
        CreateCell( row, 16, Math.Round( total, isTani ? 1 : 2 ).ToString(), xssfCellStyles[ "leftRightBottomBorderedCellStyleMediumThin" ] ) ;
      }
      else {
        CreateMergeCell( sheet, row, rowStart, rowStart, firstCellIndex, lastCellIndex, string.Join( " + ", trajectoryStr ), xssfCellStyles[ "wrapTextBorderedCellStyle" ] ) ;
        CreateCell( row, 16, Math.Round( total, isTani ? 1 : 2 ).ToString(), xssfCellStyles[ "leftRightBottomBorderedCellStyleMediumThin" ] ) ;
      }
      
      rowStart++ ;
      return rowStart ;
    }

    private int GetWidthOfCellMerge( ISheet sheet, int firstCellIndex, int lastCellIndex )
    {
      int result = 0 ;
      for ( int i = firstCellIndex ; i <= lastCellIndex ; i++ ) {
        result += sheet.GetColumnWidth( i ) ;
      }

      return result ;
    }

    private void CreateCell( IRow currentRow, int cellIndex, string value, ICellStyle style )
    {
      ICell cell = currentRow.CreateCell( cellIndex ) ;
      cell.SetCellValue( value ) ;
      cell.CellStyle = style ;
    }

    private void CreateMergeCell( ISheet sheet, IRow currentRow, int firstRowIndex, int lastRowIndex, int firstCellIndex, int lastCellIndex, string value, ICellStyle style, bool isMediumBorder = false )
    {
      ICell cell = currentRow.CreateCell( firstCellIndex ) ;
      CellRangeAddress cellMerge = new CellRangeAddress( firstRowIndex, lastRowIndex, firstCellIndex, lastCellIndex ) ;
      sheet.AddMergedRegion( cellMerge ) ;
      cell.SetCellValue( value ) ;
      cell.CellStyle = style ;
      RegionUtil.SetBorderTop( style.BorderTop == BorderStyle.None ? 0 : style.BorderTop == BorderStyle.Thin ?  1 : isMediumBorder ? 2 : 1, cellMerge, sheet ) ;
      RegionUtil.SetBorderBottom( style.BorderBottom == BorderStyle.None ? 0 : style.BorderBottom == BorderStyle.Thin ?  1 : isMediumBorder ? 2 : 1, cellMerge, sheet ) ;
      RegionUtil.SetBorderLeft( style.BorderLeft == BorderStyle.None ? 0 : style.BorderLeft == BorderStyle.Thin ?  1 : isMediumBorder ? 2 : 1, cellMerge, sheet ) ;
      RegionUtil.SetBorderRight( style.BorderRight == BorderStyle.None ? 0 : style.BorderRight == BorderStyle.Thin ?  1 : isMediumBorder ? 2 : 1, cellMerge, sheet ) ;
    }

    private XSSFCellStyle CreateCellStyle( 
      IWorkbook workbook,
      BorderStyle leftBorderStyle,
      BorderStyle rightBorderStyle, 
      BorderStyle topBorderStyle, 
      BorderStyle bottomBorderStyle, 
      NPOI.SS.UserModel.VerticalAlignment verticalAlignment, 
      NPOI.SS.UserModel.HorizontalAlignment horizontalAlignment,
      bool wrapText = false )
    {
      XSSFCellStyle borderedCellStyle = (XSSFCellStyle) workbook.CreateCellStyle() ;
      borderedCellStyle.BorderLeft = leftBorderStyle ;
      borderedCellStyle.BorderTop = topBorderStyle ;
      borderedCellStyle.BorderRight = rightBorderStyle ;
      borderedCellStyle.BorderBottom = bottomBorderStyle ;
      borderedCellStyle.VerticalAlignment = verticalAlignment ;
      borderedCellStyle.Alignment = horizontalAlignment ;
      borderedCellStyle.WrapText = wrapText ;
      
      XSSFFont myFont = (XSSFFont) workbook.CreateFont() ;
      myFont.FontName = "ＭＳ 明朝";
      borderedCellStyle.SetFont( myFont );
      return borderedCellStyle ;
    }
    
    private void OutputItemsSelectionSetting()
    {
      var settingOutputPickUpReport = new SettingOutputPickUpReport( this ) ;
      settingOutputPickUpReport.ShowDialog();

      if ( settingOutputPickUpReport.DialogResult == false ) {
        CurrentSettingList = new ObservableCollection<ListBoxItem>( PreviousSettingList.Select(x => x.Copy()).ToList() ) ;
      }
      else {
        PreviousSettingList = new ObservableCollection<ListBoxItem>( CurrentSettingList.Select(x => x.Copy()).ToList() ) ;
      }
    }
    
    private void SetOption( Window window )
    {
      window.DialogResult = true ;
      window.Close() ;
    }

    private void GetPickModels()
    {
      if ( ! IsOutputItemsEnable )
        InitPickUpModels() ;
      else
        UpdatePickModels() ;
    }

    private void UpdatePickModels()
    {
      var settings = CurrentSettingList.Where( s => s.TheValue ).Select( s => s.TheText ) ;

      var newPickUpModels = PickUpModels
        .Where(p=> 
          _hiroiMasterModels.Any( h => 
            (int.Parse( h.Buzaicd ) ==  int.Parse( p.ProductCode.Split( '-' ).First())) 
            && (settings.Contains( h.Syurui )) )) ;

      PickUpModels = new ObservableCollection<PickUpModel>( newPickUpModels ) ;
    }

    private bool IsTani( PickUpModel pickUpModel )
    {
      return pickUpModel.Tani == "m" ;
    }
    
    public class ListBoxItem
    {
      public string? TheText { get ; set ; }
      public bool TheValue { get ; set ; }
    }
  }
}