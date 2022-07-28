﻿using System.Collections.Generic ;
using Arent3d.Architecture.Routing.Storable.Model ;
using Autodesk.Revit.DB ;
using System ;
using System.Collections.ObjectModel ;
using System.Collections.Specialized ;
using System.Globalization ;
using System.IO ;
using System.Linq ;
using System.Reflection ;
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
using Arent3d.Architecture.Routing.StorableCaches ;
using MoreLinq ;
using MoreLinq.Extensions ;
using NPOI.OpenXmlFormats.Spreadsheet ;
using CellType = NPOI.SS.UserModel.CellType ;
using FillPattern = NPOI.SS.UserModel.FillPattern ;
using MarginType = NPOI.SS.UserModel.MarginType ;


namespace Arent3d.Architecture.Routing.AppBase.ViewModel
{
  public class PickUpReportViewModel : NotifyPropertyChanged
  {
    private const string SummaryFileType = "拾い出し集計表" ;
    private const string ConfirmationFileType = "拾い根拠確認表" ;
    private string PickUpNumberOff => FileName + "OFF" ;
    private string PickUpNumberOn => FileName + "ON" ;
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
    private const string Wire = "電線" ;
    private const float DefaultCharacterWidth = 7.001699924468994F ;
    private const string SummaryTemplateFileName = "拾い出し集計表_template.xls" ;
    private const string ConfirmationTemplateFileName = "拾い根拠確認表_template.xls" ;
    private const string ResourceFolderName = "resources" ;
    
    private readonly Document _document ;
    private readonly List<HiroiMasterModel> _hiroiMasterModels ;
    private readonly List<HiroiSetMasterModel> _hiroiSetMasterNormalModels ;
    private readonly List<HiroiSetMasterModel> _hiroiSetMasterEcoModels ;
    private readonly List<HiroiSetCdMasterModel> _hiroiSetCdMasterNormalModels ;
    private readonly List<HiroiSetCdMasterModel> _hiroiSetCdMasterEcoModels ;
    private readonly List<DetailTableModel> _dataDetailTable ;

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
      _hiroiSetMasterNormalModels = new List<HiroiSetMasterModel>() ;
      _hiroiSetMasterEcoModels = new List<HiroiSetMasterModel>() ;
      _hiroiSetCdMasterNormalModels = new List<HiroiSetCdMasterModel>() ;
      _hiroiSetCdMasterEcoModels = new List<HiroiSetCdMasterModel>() ;
      _dataDetailTable = new List<DetailTableModel>() ;
      
      var csvStorable = _document.GetAllStorables<CsvStorable>().FirstOrDefault() ;
      if ( csvStorable != null ) 
      {
        _hiroiMasterModels = csvStorable.HiroiMasterModelData ;
        _hiroiSetMasterNormalModels = csvStorable.HiroiSetMasterNormalModelData ;
        _hiroiSetMasterEcoModels = csvStorable.HiroiSetMasterEcoModelData ;
        _hiroiSetCdMasterNormalModels = csvStorable.HiroiSetCdMasterNormalModelData ;
        _hiroiSetCdMasterEcoModels = csvStorable.HiroiSetCdMasterEcoModelData ;
      }
      
      var detailSymbolStorable =  _document.GetDetailTableStorable() ;
      _dataDetailTable = detailSymbolStorable.DetailTableModelData ; ;


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
      if ( _fileNames.Any() && ! string.IsNullOrEmpty( PathName ) && ! string.IsNullOrEmpty( FileName ) && PickUpModels.Any()  ) {
        CreateOutputFile() ;
        window.DialogResult = true ;
        window.Close() ;
      }
      else { 
        var errorStr = "Please select " ;
        var listError = new List<string>();
        if ( ! _fileNames.Any() ) listError.Add( "file type" ) ; 
        if ( string.IsNullOrEmpty( PathName ) ) listError.Add( "the output folder" ) ;
        if ( string.IsNullOrEmpty( FileName ) ) listError.Add( "the file name" ) ;
        for ( int i = 0 ; i < listError.Count ; i++ ) {
          if ( i != listError.Count - 1 ) errorStr += listError[ i ] + ( i == listError.Count - 2 ? " " : ", " ) ;
          else errorStr += $"and {listError[ i ]}." ;
        }
        if ( ! PickUpModels.Any() ) errorStr = "Don't have pick up data." ;
        MessageBox.Show( errorStr, "Warning" ) ;
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
      var pickUpNumberStatus = radioButton!.Content.ToString() == OnText ? PickUpNumberOn : PickUpNumberOff ;
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
        _fileNames.Add( pickUpNumberStatus + fileName ) ;
      }
      
    }

    public void FileTypeChecked( object sender )
    {
      var checkbox = sender as CheckBox ;
      var pickUpNumberStatus = DoconTypes.First().TheValue ? PickUpNumberOn : PickUpNumberOff ;
      switch ( checkbox!.Content.ToString() ) {
        case SummaryFileType :
          if ( ! _fileNames.Contains( pickUpNumberStatus + SummaryFileName ) )
            _fileNames.Add( pickUpNumberStatus + SummaryFileName ) ;
          break ;
        case ConfirmationFileType :
          if ( ! _fileNames.Contains( pickUpNumberStatus + ConfirmationFileName ) )
            _fileNames.Add( pickUpNumberStatus + ConfirmationFileName ) ;
          break ;
      }
      
    }

    public void FileTypeUnchecked( object sender )
    {
      var checkbox = sender as CheckBox ;
      var pickUpNumberStatus = DoconTypes.First().TheValue ? PickUpNumberOn : PickUpNumberOff ;
      switch ( checkbox!.Content.ToString() ) {
        case SummaryFileType :
          if ( _fileNames.Contains( pickUpNumberStatus + SummaryFileName ) )
            _fileNames.Remove( pickUpNumberStatus + SummaryFileName ) ;
          break ;
        case ConfirmationFileType :
          if ( _fileNames.Contains( pickUpNumberStatus + ConfirmationFileName ) )
            _fileNames.Remove( pickUpNumberStatus + ConfirmationFileName ) ;
          break ;
      }
    }

    private string GetFileName(string fileName)
    {
      return string.IsNullOrEmpty( _fileName ) ? fileName : $"{_fileName}{fileName}" ;
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
      if ( ! PickUpModels.Any() ) return;
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
            { "leftRightBorderedCellStyle", CreateCellStyle( workbook, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.None, BorderStyle.None, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Right ) },
            { "exceptTopBorderedCellStyle", CreateCellStyle( workbook, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.None, BorderStyle.Thin, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Left ) },
            { "wrapTextBorderedCellStyle", CreateCellStyle( workbook, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Thin, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Left, true ) },
            { "borderedCellStyleMedium", CreateCellStyle( workbook, BorderStyle.Medium, BorderStyle.Medium, BorderStyle.Medium, BorderStyle.Medium, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Center ) },
            { "bottomBorderedCellStyleMedium", CreateCellStyle( workbook, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.None, BorderStyle.Medium, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Left ) },
            { "topBorderedCellStyleMedium", CreateCellStyle( workbook, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Medium, BorderStyle.Thin, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Center ) },
            { "leftBottomBorderedCellStyleMedium", CreateCellStyle( workbook, BorderStyle.Medium, BorderStyle.None, BorderStyle.None, BorderStyle.Thin, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Left ) },
            { "leftBottomBorderedCellStyleMediumWrap", CreateCellStyle( workbook, BorderStyle.Medium, BorderStyle.None, BorderStyle.None, BorderStyle.Thin, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Left, true ) },
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
            { "rightBorderedCellStyleMediumDotted", CreateCellStyle( workbook, BorderStyle.None, BorderStyle.Medium, BorderStyle.None, BorderStyle.None, NPOI.SS.UserModel.VerticalAlignment.Center, NPOI.SS.UserModel.HorizontalAlignment.Right ) },
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
          {
            string resourcesPath = Path.Combine( Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location )!, ResourceFolderName ) ;
            var filePath = Path.Combine( resourcesPath, SummaryTemplateFileName) ;
            using (FileStream fsStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
              workbook = new XSSFWorkbook(fsStream);
            }
            
            foreach ( var sheetName in constructionItemList ) {
              var sheetCopy = workbook.GetSheetAt( 0 ).CopySheet( sheetName, true ) ;
              CreateSheet( SheetType.Summary, workbook, sheetCopy, sheetName ) ;
              workbook.RemoveSheetAt( 0 );
            }
          }
            
          else if ( fileName.Contains( ConfirmationFileName ) ) 
          {
            string resourcesPath = Path.Combine( Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location )!, ResourceFolderName ) ;
            var filePath = Path.Combine( resourcesPath, ConfirmationTemplateFileName) ;
            using (FileStream fsStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
              workbook = new XSSFWorkbook(fsStream);
            }
            foreach ( var sheetName in constructionItemList ) {
              var sheetCopy = workbook.GetSheetAt( 0 ).CopySheet( sheetName, true ) ;
              CreateSheet( SheetType.Confirmation, workbook, sheetCopy, sheetName ) ;
              workbook.RemoveSheetAt( 0 );
            }
          }
          
          var fileNameToOutPut = GetFileName(fileName) ;
          FileStream fs = new FileStream( PathName + @"\" + fileNameToOutPut, FileMode.OpenOrCreate ) ;
          workbook.Write( fs ) ;
          workbook.Close() ;
          workbook.Close();
          fs.Close() ;
        }

        MessageBox.Show( "Export pick-up output file successfully.", "Message" ) ;
      }
      catch ( Exception ex ) {
        MessageBox.Show( "Export file failed because " + ex, "Error message" ) ;
      }
    }
    
    void SaveWorkbook(IWorkbook workbook, string path)
    {
      using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write))
      {
        workbook.Write(fileStream);
      }
    }

    private enum SheetType
    {
      Confirmation,
      Summary
    }

    private void CreateSheet( SheetType sheetType, IWorkbook workbook, ISheet sheet, string sheetName)
    {
      List<string> levels = _document.GetAllElements<Level>().Select( l => l.Name ).ToList() ;
      var codeList = GetCodeList() ;
      var fileName = FileName ;
      IRow row0, row2 ;
      int rowStart ;
      switch ( sheetType ) {
        case SheetType.Confirmation :
          rowStart = 0 ;
          var view = _document.ActiveView ;
          var scale = view.Scale ;
          HeightSettingStorable settingStorables = _document.GetHeightSettingStorable() ;
          var number = NumberLevelHaveValue( levels ) ;
          var numberCount = 0 ;
          
          foreach ( var level in levels ) {
            if( PickUpModels.All( x => x.Floor != level ) ) continue;
            if( PickUpModels.Where( x => x.Floor == level ).All( p => p.ConstructionItems != sheetName ) ) continue;
            numberCount++ ;
            var height = settingStorables.HeightSettingsData.Values.FirstOrDefault( x => x.LevelName == level )?.Elevation ?? 0 ;
            row0 = sheet.GetRow( rowStart ) ;
            var row1 = sheet.GetRow( rowStart + 1 ) ;
            row2 = sheet.GetRow( rowStart + 2 ) ;
            SetCellValue( row0, 2 , fileName ) ;
            SetCellValue( row0, 13, $"縮尺:" ) ;
            SetCellValue( row0, 14, $"1/{scale}" ) ;
            SetCellValue( row0, 15, $"階高:" ) ;
            SetCellValue( row0, 16, $"{Math.Round(height/1000,1)}ｍ" ) ;
            SetCellValue( row1, 1, "【入力確認表】" ) ;
            SetCellValue( row1, 2, "工事項目:" ) ;
            SetCellValue( row1, 3, sheetName ) ;
            SetCellValue( row1, 7, "図面番号:" ) ;
            SetCellValue( row1, 10, "階数:") ;
            SetCellValue( row1, 11, level) ;
            SetCellValue( row1, 12, "区間:") ;
            var space = "      " ;
            SetCellValue( row2, 1, $"品{space}名" ) ;
            SetCellValue( row2, 2,  $"規{space}格" ) ;
            SetCellValue( row2, 4, "単位" ) ;
            var nextSpace = "                " ;
            SetCellValue( row2, 5, $"軌{nextSpace}跡" ) ;
            SetCellValue( row2, 16, "合計数量") ;

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
            var pickUpNumberForConduitsToPullBox = GetPickUpNumberForConduitsToPullBox(_document,PickUpModels.Where( p=> p.Floor == level ).ToList()) ;

            int numberRowOfLevel = 3 ;
            foreach ( var dataPickUpModel in dictionaryDataPickUpModelOrder ) {
              numberRowOfLevel = GetRowOfLevel( dataPickUpModel.Value, sheet, numberRowOfLevel, pickUpNumberForConduitsToPullBox ) ;
            }

            var parts = numberRowOfLevel / 62 ;
            for ( int i = 1 ; i <= parts ; i++ ) {
              while ( rowStart % 62 != 0 ) {
                for ( int j = 1 ; j <= 62 ; j++ ) {
                  CopyFromSourceToDestinationRow( workbook, workbook.GetSheetAt( 0 ), sheet, i, rowStart ) ;
                  rowStart++ ;
                  i++ ;
                }
              }
            }
            
            foreach ( var dataPickUpModel in dictionaryDataPickUpModelOrder ) {
              rowStart = AddConfirmationPickUpRow( dataPickUpModel.Value, sheet, rowStart, pickUpNumberForConduitsToPullBox ) ;
            }
            
            while ( rowStart < 61 ) {
              rowStart++ ;
            }
            
            rowStart += 1 ;
            
            if ( numberCount != number ) {
              int i = 0 ;
              var tempRowStart = rowStart ;
              for ( int j = 1 ; j <= 62 ; j++ ) {
                CopyFromSourceToDestinationRow( workbook, workbook.GetSheetAt( 0 ), sheet, i, tempRowStart ) ;
                tempRowStart++ ;
                i++ ;
              }
            }
          }
          
          break ;
        case SheetType.Summary :
         
          row0 = sheet.GetRow( 0 ) ;
          row2 = sheet.GetRow( 2 ) ;
          var row3 = sheet.GetRow( 3 ) ;
          SetCellValue( row0, 2, "【拾い出し集計表】" ) ;
          SetCellValue(  row0, 6,  fileName ) ;
          
          SetCellValue( row0, 14, sheetName ) ;
          Dictionary<int, string> levelColumns = new Dictionary<int, string>() ;
          var index = 5 ;
          foreach ( var level in levels ) {
            if(PickUpModels.All( x => x.Floor != level )) continue;
            SetCellValue( row2, index, level) ;
            levelColumns.Add( index, level ) ;
            index++ ;
          }
          
          SetCellValue( row2, index, "合計") ;
          
          var spaceS = "  " ;
          SetCellValue( row3, 1,  $"品{spaceS}名 / 規{spaceS}格") ;
          SetCellValue( row3, 4, "単位" ) ;
          
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
              rowStart = AddSummaryPickUpRow( dataPickUpModel.Value, sheet, rowStart, levelColumns, index ) ;
            }
            else {
              rowStart = AddSummaryPickUpRow( dataPickUpModel.Value, sheet, rowStart, levelColumns, index ) ;
            }
          }
          
          break ;
      }
    }

    private int NumberLevelHaveValue( List<string> levels )
    {
      List<string> result = new List<string>() ;
      foreach ( var level in levels ) {
        if ( PickUpModels.Any( x => x.Floor == level ) && !result.Contains( level ) ) result.Add( level ) ;
      }

      return result.Count ;
    }

    private void CreateTemplateForFloor(ISheet sourceSheet, ISheet destinationSheet, int rowStart)
    {
      for ( int i = rowStart ; i < rowStart + 61 ; i++ ) {
        //destinationSheet.GetRow( rowStart ) = sourceSheet.GetRow( 0 ).Cop ;
      }
    }
    
    private void CopyFromSourceToDestinationRow(IWorkbook workbook, ISheet sourceWorksheet, ISheet destinationWorksheet, int sourceRowNum, int destinationRowNum) {
        // Get the source / new row
        XSSFRow sourceRow = (XSSFRow)sourceWorksheet.GetRow(sourceRowNum);
        XSSFRow newRow = (XSSFRow)destinationWorksheet.CreateRow(destinationRowNum);
        // Loop through source columns to add to new row
        for (int i = 0; i < sourceRow.LastCellNum; i++) {
            // Grab a copy of the old/new cell
            XSSFCell oldCell = (XSSFCell)sourceRow.GetCell(i);
            XSSFCell newCell = (XSSFCell)newRow.CreateCell(i);
            // If the old cell is null jump to next cell
            if (oldCell == null) {
                continue;
            }

            // Copy style from old cell and apply to new cell
            XSSFCellStyle newCellStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            newCellStyle.CloneStyleFrom(oldCell.CellStyle);
            newCell.CellStyle = newCellStyle ;

            // If there is a cell comment, copy
            if (oldCell.CellComment != null) {
              newCell.CellComment = oldCell.CellComment ;
            }

            // If there is a cell hyperlink, copy
            if (oldCell.Hyperlink != null) {
              newCell.Hyperlink = oldCell.Hyperlink ;
            }

            // Set the cell data type
            newCell.SetCellType( oldCell.CellType ) ;

            // Set the cell data value
            switch (oldCell.CellType) {
            case CellType.Blank:// Cell.CELL_TYPE_BLANK:
                newCell.SetCellValue( oldCell.StringCellValue );
                break;
            case CellType.Boolean:
                newCell.SetCellValue( oldCell.BooleanCellValue );
                break;
            case CellType.Formula:
                newCell.SetCellValue( oldCell.CellFormula );
                break;
            case CellType.Numeric:
                newCell.SetCellValue( oldCell.NumericCellValue );
                break;
            case CellType.String:
                newCell.SetCellValue( oldCell.StringCellValue );
                break;
            }
        }

        // If there are are any merged regions in the source row, copy to new row
        for (int i = 0; i < sourceWorksheet.NumMergedRegions; i++) {
            CellRangeAddress cellRangeAddress = sourceWorksheet.GetMergedRegion(i);
            if (cellRangeAddress.FirstRow == sourceRow.RowNum) {
                CellRangeAddress newCellRangeAddress = new CellRangeAddress(newRow.RowNum,
                        (newRow.RowNum + (cellRangeAddress.LastRow - cellRangeAddress.FirstRow)),
                        cellRangeAddress.FirstColumn, cellRangeAddress.LastColumn);
                destinationWorksheet.AddMergedRegion(newCellRangeAddress);
            }
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
      bool isLastRow = false
      )
    {
      if ( ! pickUpModels.Any() ) return rowStart ;
      var pickUpModel = pickUpModels.First() ;
      var rowName = sheet.GetRow( rowStart ) ;
      var isTani = IsTani( pickUpModel ) ;
      SetCellValue( rowName, 1, pickUpModel.ProductName ) ;
      SetCellValue( rowName, 4, isTani ? "ｍ" : pickUpModel.Tani ) ;

      rowStart++ ;
      var rowStandard = sheet.GetRow( rowStart ) ;
      SetCellValue( rowStandard, 2, pickUpModel.Standard) ;

      double total = 0 ;
      for ( var i = 5 ; i < index ; i++ ) {
        double quantityFloor = 0 ;
        var level = levelColumns[ i ] ;
        quantityFloor = CalculateTotalByFloor( pickUpModels.Where( item => item.Floor == level ).ToList() ) ;
        SetCellValue( rowName, i, quantityFloor == 0 ? string.Empty : $"{Math.Round( quantityFloor, isTani ? 1 : 2 )}" ) ;
        total += quantityFloor ;
      }
      
      SetCellValue( rowName, index, total == 0 ? string.Empty : $"{Math.Round( total, isTani ? 1 : 2 )}") ;
      
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
    
    private int AddConfirmationPickUpRow( List<PickUpModel> pickUpModels, ISheet sheet, int rowStart, IReadOnlyDictionary<string, int> pickUpNumberForConduitsToPullBox )
    {
      if ( ! pickUpModels.Any() ) return rowStart ;
      var pickUpNumbers = GetPickUpNumbersList( pickUpModels ) ;
      var pickUpModel = pickUpModels.First() ;
      var row = sheet.GetRow( rowStart ) ;
      var isTani = IsTani( pickUpModel ) ;
      double total = 0 ;
      Dictionary<string, int> trajectory = new Dictionary<string, int>() ;
      var routes = RouteCache.Get( DocumentKey.Get( _document ) ) ;
      var inforDisplays = GetInforDisplays( pickUpModels, routes ) ;
      var isMoreTwoWireBook = IsMoreTwoWireBook( pickUpModels ) ;
      SetCellValue( row, 1, pickUpModel.ProductName ) ;
      SetCellValue( row, 2, pickUpModel.Standard ) ;
      SetCellValue( row, 4, isTani ? "ｍ" : pickUpModel.Tani) ;
      foreach ( var pickUpNumber in pickUpNumbers ) {
        string stringNotTani = string.Empty ;
        Dictionary<string, double> notSeenQuantities = new Dictionary<string, double>() ;
        Dictionary<string, double> notSeenQuantitiesPullBox = new Dictionary<string, double>() ;
        var items = pickUpModels.Where( p => p.PickUpNumber == pickUpNumber ).ToList() ;
        var itemFirst = items.First() ;
        var wireBook = ( string.IsNullOrEmpty( itemFirst.WireBook ) || itemFirst.WireBook == "1" ) ? string.Empty : itemFirst.WireBook ;
        var itemsGroupByRoute = items.Where( item => ! string.IsNullOrEmpty( item.Quantity ) ).GroupBy( i => i.RelatedRouteName ) ;
        var listSeenQuantity = new List<double>() ;
        var listSeenQuantityPullBox = new List<string>() ;
        var valueDetailTableStr = string.Empty ;
        double totalBasedOnCreateTable = 0 ;
        foreach ( var itemGroupByRoute in itemsGroupByRoute ) {
          valueDetailTableStr = wireBook ;
          double seenQuantity = 0 ;

          var lastSegment = GetLastSegment( itemGroupByRoute.Key, routes ) ;
          var isSegmentConnectedToPullBox = IsSegmentConnectedToPullBox( lastSegment ) ;
          var isSegmentFromPowerToPullBox = IsSegmentFromPowerToPullBox( lastSegment ) ;
          var pickUpNumberForConduitToPullBox = pickUpNumberForConduitsToPullBox.SingleOrDefault( p => p.Key == itemGroupByRoute.Key ) ;
          var pickUpNumberPullBox = pickUpNumberForConduitToPullBox.Value ;

          foreach ( var item in itemGroupByRoute ) {
            double.TryParse( item.Quantity, out var quantity ) ;
            if ( ! string.IsNullOrEmpty( item.Direction ) ) {
              if ( isSegmentFromPowerToPullBox ) {
                if ( ! notSeenQuantitiesPullBox.Keys.Contains( item.Direction ) ) {
                  notSeenQuantitiesPullBox.Add( item.Direction, 0 ) ;
                }

                notSeenQuantitiesPullBox[ item.Direction ] += quantity ;
              }
              else {
                if ( ! notSeenQuantities.Keys.Contains( item.Direction ) ) {
                  notSeenQuantities.Add( item.Direction, 0 ) ;
                }

                notSeenQuantities[ item.Direction ] += quantity ;
              }
            }
            else {
              if ( ! isTani ) stringNotTani += string.IsNullOrEmpty( stringNotTani ) ? item.SumQuantity : $"＋{item.SumQuantity}" ;
              seenQuantity += quantity ;
            }

            totalBasedOnCreateTable += quantity ;
          }
          
          if ( seenQuantity > 0 ) {
            if ( isSegmentConnectedToPullBox ) {
              var countStr = string.Empty ;
              var inforDisplay = inforDisplays.SingleOrDefault( x => x.RouteNameRef == itemGroupByRoute.Key ) ;
              var numberPullBox = DoconTypes.First().TheValue ? $"[{(pickUpNumberPullBox)}]" : string.Empty;
              if ( inforDisplay != null && inforDisplay.IsDisplay) {
                countStr = ( inforDisplay.NumberDisplay == 1 ? string.Empty : $"×{inforDisplay.NumberDisplay}" ) + ( isMoreTwoWireBook ? $"×N" : string.IsNullOrEmpty(valueDetailTableStr) ? string.Empty: $"×{valueDetailTableStr}" ) ;
                inforDisplay.IsDisplay = false ;
                if ( isSegmentFromPowerToPullBox ) {
                  listSeenQuantityPullBox.Add(numberPullBox + $"({Math.Round( seenQuantity, isTani ? 1 : 2 ).ToString( CultureInfo.InvariantCulture )}＋↓{Math.Round( notSeenQuantitiesPullBox.First().Value, isTani ? 1 : 2 )})" + countStr);
                }
                else {
                  listSeenQuantityPullBox.Add( numberPullBox + Math.Round( seenQuantity, isTani ? 1 : 2 ).ToString( CultureInfo.InvariantCulture ) + countStr);
                }
              }
            }
            else {
              listSeenQuantity.Add( Math.Round( seenQuantity, isTani ? 1 : 2 ) ) ;
            }
          }
        }

        total += ! string.IsNullOrEmpty( wireBook ) ? Math.Round( totalBasedOnCreateTable, isTani ? 1 : 2 ) * int.Parse( wireBook ) : Math.Round( totalBasedOnCreateTable, isTani ? 1 : 2 ) ;

        var number = DoconTypes.First().TheValue && ! string.IsNullOrEmpty( pickUpNumber ) ? "[" + pickUpNumber + "]" : string.Empty ;
        var seenQuantityStr = isTani ? string.Join( "＋", listSeenQuantity ) : string.Join("＋",stringNotTani.Split( '+' )) ;

        var seenQuantityPullBoxStr = string.Empty ;
        if ( listSeenQuantityPullBox.Any() ) {
          seenQuantityPullBoxStr = string.Join($"＋", listSeenQuantityPullBox ) ;
        }
        
        var notSeenQuantityStr = string.Empty ;
        foreach ( var (_, value) in notSeenQuantities ) {
          notSeenQuantityStr += value > 0 ? "＋↓" + Math.Round( value, isTani ? 1 : 2 ) : string.Empty ;
        }

        var key = isTani ? ( "(" + seenQuantityStr + notSeenQuantityStr + ")" ) + (string.IsNullOrEmpty( valueDetailTableStr ) ? string.Empty : $"×{valueDetailTableStr}") + (string.IsNullOrEmpty( seenQuantityPullBoxStr ) ? string.Empty : $"＋{seenQuantityPullBoxStr}" )  : ( seenQuantityStr + notSeenQuantityStr ) ;
        var itemKey = trajectory.FirstOrDefault( t => t.Key.Contains( key ) ).Key ;

        if ( string.IsNullOrEmpty( itemKey ) )
          trajectory.Add( number + key, 1 ) ;
        else {
          trajectory[ itemKey ]++ ;
        }
      }
      
      List<string> trajectoryStr = ( from item in trajectory select item.Value == 1 ? item.Key : item.Key + "×" + item.Value ).ToList() ;
      int lengthOfCellMerge = GetWidthOfCellMerge( sheet, 5, 15 ) ;
      
      var valueOfCell = string.Empty ;
      var trajectoryStrCount = trajectoryStr.Count ;
      var count = 0 ;
      if ( trajectoryStrCount > 1 ) {
        for ( var i = 0 ; i < trajectoryStrCount ; i++ ) {
          valueOfCell += trajectoryStr[ i ] + (i == trajectoryStrCount - 1 ? "": "＋");
          if ( valueOfCell.Length * 2.5  < lengthOfCellMerge/256.0 && i < trajectoryStrCount - 1 ) continue;
          if ( count == 0 ) {
            SetCellValue( row,5, valueOfCell ) ;
            count++ ;
          }
          else {
            var rowTrajectory = sheet.GetRow( ++rowStart ) ;
            SetCellValue( rowTrajectory, 5, valueOfCell  ) ;
          }

          valueOfCell = string.Empty ;
        }
        SetCellValue( row, 16, $"{Math.Round( total, isTani ? 1 : 2 )}" ) ;
      }
      else {
        SetCellValue( row, 5, string.Join( "＋", trajectoryStr ) ) ;
        SetCellValue( row, 16, $"{Math.Round( total, isTani ? 1 : 2 )}"  ) ;
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
      style.DataFormat = (short)49 ; // format type is text
      cell.CellStyle = style ;
      cell.SetCellValue( value ) ;
    }
    
    private void SetCellValue( IRow currentRow, int cellIndex, string value )
    {
      ICell cell = currentRow.GetCell( cellIndex ) ;
      cell.SetCellValue( value ) ;
    }

    private void CreateMergeCell( ISheet sheet, IRow currentRow, int firstRowIndex, int lastRowIndex, int firstCellIndex, int lastCellIndex, string value, ICellStyle style, bool isMediumBorder = false )
    {
      ICell cell = currentRow.CreateCell( firstCellIndex ) ;
      CellRangeAddress cellMerge = new CellRangeAddress( firstRowIndex, lastRowIndex, firstCellIndex, lastCellIndex ) ;
      sheet.AddMergedRegion( cellMerge ) ;
      style.DataFormat = (short)49 ; // format type is text
      style.FillBackgroundColor = NPOI.HSSF.Util.HSSFColor.White.Index ;
      cell.CellStyle = style ;
      cell.SetCellValue( value ) ;
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
    
    private RouteSegment? GetLastSegment( string routeName, RouteCache routes )
    {
      if ( string.IsNullOrEmpty( routeName ) ) return null ;
      var route = routes.SingleOrDefault( x => x.Key == routeName ) ;
      return route.Value.RouteSegments.LastOrDefault();
    }
    
    private bool IsSegmentConnectedToPullBox( RouteSegment? lastSegment )
    {
      if ( lastSegment == null ) return false ;
      var toEndPointKey = lastSegment.ToEndPoint.Key ;
      var toElementId = toEndPointKey.GetElementUniqueId() ;
      if ( string.IsNullOrEmpty( toElementId ) ) 
        return false ;
      var toConnector = _document.GetAllElements<FamilyInstance>().OfCategory( BuiltInCategory.OST_ElectricalFixtures )
        .FirstOrDefault( c => c.UniqueId == toElementId ) ;
      return toConnector != null && toConnector.GetConnectorFamilyType() == ConnectorFamilyType.PullBox ;
    }
    
    private bool IsSegmentFromPowerToPullBox( RouteSegment? lastSegment )
    {
      if ( lastSegment == null ) return false ;
      var fromEndPointKey = lastSegment.FromEndPoint.Key ;
      var toEndPointKey = lastSegment.ToEndPoint.Key ;
      var fromElementId = fromEndPointKey.GetElementUniqueId() ;
      var toElementId = toEndPointKey.GetElementUniqueId() ;
      if ( string.IsNullOrEmpty( toElementId ) || string.IsNullOrEmpty( fromElementId ) ) 
        return false ;
      var fromConnector = _document.GetAllElements<FamilyInstance>().OfCategory( BuiltInCategory.OST_ElectricalFixtures )
        .FirstOrDefault( c => c.UniqueId == fromElementId ) ;
      var toConnector = _document.GetAllElements<FamilyInstance>().OfCategory( BuiltInCategory.OST_ElectricalFixtures )
        .FirstOrDefault( c => c.UniqueId == toElementId ) ;
      return fromConnector != null && toConnector != null && fromConnector.GetConnectorFamilyType() == ConnectorFamilyType.Power && toConnector.GetConnectorFamilyType() == ConnectorFamilyType.PullBox;
    }
    
    private List<InforDisplay> GetInforDisplays(List<PickUpModel> pickUpModels, RouteCache routes)
    {
      var routesNameRef = pickUpModels.Select( x => x.RelatedRouteName ).Distinct() ;
      var inforDisplays = new List<InforDisplay>() ;
      foreach ( var routeNameRef in routesNameRef ) {
        var lastSegment = GetLastSegment( routeNameRef, routes ) ;
        if ( lastSegment == null ) continue ;
        if ( IsSegmentConnectedToPullBox( lastSegment ) ) {
          var idPullBox = lastSegment.FromEndPoint.Key.GetElementUniqueId() ;
          if ( inforDisplays.Any( x => x.IdPullBox == idPullBox ) ) {
            var inforDisplay = inforDisplays.SingleOrDefault( x => x.IdPullBox == idPullBox ) ;
            if ( inforDisplay == null ) continue;
            inforDisplay.NumberDisplay += 1 ;
            inforDisplay.RouteNameRef = routeNameRef ;
          }
          else {
            inforDisplays.Add( new InforDisplay(true, idPullBox, 1, routeNameRef) );
          }
        }
      }

      return inforDisplays ;
    }

    private int GetWidth256Excel( float widthExcel )
    {
      return (int)Math.Round((widthExcel * DefaultCharacterWidth + 5) / DefaultCharacterWidth * 256);
    }
    
    private bool IsMoreTwoWireBook( List<PickUpModel> pickUpModels )
    {
      var pickUpNumbers = GetPickUpNumbersList( pickUpModels ) ;
      if ( pickUpModels.Count < 2 ) return false ;
      var wireBooks = new List<string>() ;
      foreach ( var pickUpNumber in pickUpNumbers ) {
        var items = pickUpModels.Where( p => p.PickUpNumber == pickUpNumber ).ToList() ;
        var itemFirst = items.First() ;
        var wireBook = itemFirst.WireBook ;
        int wireBookInt ;
        if ( int.TryParse( wireBook, out wireBookInt ) && wireBookInt > 1 && ! wireBooks.Contains( wireBook ) ) {
          wireBooks.Add( wireBook );
        }
      }
      
      return wireBooks.Count > 1 ;
    }

    private double CalculateTotalByFloor(List<PickUpModel> pickUpModels)
    {
      double result = 0 ;
      if ( pickUpModels.Count < 1 ) return result ;
      if ( IsTani( pickUpModels.First() ) ) {
        var pickUpModelsByNumber = PickUpModelByNumber( PickUpViewModel.ProductType.Conduit, pickUpModels ) ;
        foreach ( var pickUpModelByNumber in pickUpModelsByNumber ) {
          var wireBook = pickUpModelByNumber.WireBook ;
          if ( ! string.IsNullOrEmpty( wireBook ) ) {
            result += ( double.Parse( pickUpModelByNumber.Quantity ) * int.Parse( wireBook ) ) ;
          }
          else {
            result += double.Parse( pickUpModelByNumber.Quantity ) ;
          }
        }
      }
      else {
        foreach ( var item in pickUpModels ) {
          double.TryParse( item.Quantity, out var quantity ) ;
          result += quantity ;
        }
      }
     
      return result ;
    }
    
    private List<PickUpModel> PickUpModelByNumber( PickUpViewModel.ProductType productType, List<PickUpModel> pickUpModels )
    {
      List<PickUpModel> result = new() ;
      
      var equipmentType = productType.GetFieldName() ;
      var pickUpModelsByNumber = pickUpModels.Where( p => p.EquipmentType == equipmentType )
        .GroupBy( x => x.PickUpNumber )
        .Select( g => g.ToList() ) ;
      
      foreach ( var pickUpModelByNumber in pickUpModelsByNumber ) {
        var pickUpModelByProductCodes = PickUpModelByProductCode( pickUpModelByNumber ) ;
        result.AddRange(pickUpModelByProductCodes);
      }

      return result ;
    }
    
    private List<PickUpModel> PickUpModelByProductCode( List<PickUpModel> pickUpModels )
    {
      List<PickUpModel> pickUpModelByProductCodes = new() ;
      
      var pickUpModelsByProductCode = pickUpModels.GroupBy( x => x.ProductCode.Split( '-' ).First() )
        .Select( g => g.ToList() ) ;
        
      foreach ( var pickUpModelByProductCode in pickUpModelsByProductCode ) {
        var pickUpModelsByConstructionItemsAndConstruction = pickUpModelByProductCode.GroupBy( x => ( x.ConstructionItems, x.Construction ) )
          .Select( g => g.ToList() ) ;
          
        foreach ( var pickUpModelByConstructionItemsAndConstruction in pickUpModelsByConstructionItemsAndConstruction ) {
          var sumQuantity = Math.Round(pickUpModelByConstructionItemsAndConstruction.Sum( p => Convert.ToDouble( p.Quantity ) ), 1) ;
            
          var pickUpModel = pickUpModelByConstructionItemsAndConstruction.FirstOrDefault() ;
          if ( pickUpModel == null ) 
            continue ;
            
          PickUpModel newPickUpModel = new(pickUpModel.Item, pickUpModel.Floor, pickUpModel.ConstructionItems, pickUpModel.EquipmentType, pickUpModel.ProductName, pickUpModel.Use, pickUpModel.UsageName, pickUpModel.Construction, pickUpModel.ModelNumber, pickUpModel.Specification, pickUpModel.Specification2, pickUpModel.Size, $"{sumQuantity}", pickUpModel.Tani, pickUpModel.Supplement, pickUpModel.Supplement2, pickUpModel.Group, pickUpModel.Layer, pickUpModel.Classification, pickUpModel.Standard, pickUpModel.PickUpNumber, pickUpModel.Direction, pickUpModel.ProductCode, pickUpModel.CeedSetCode, pickUpModel.DeviceSymbol, pickUpModel.Condition, pickUpModel.SumQuantity, pickUpModel.RouteName, null, pickUpModel.WireBook ) ;
          
          pickUpModelByProductCodes.Add( newPickUpModel ) ;
        }
      }

      return pickUpModelByProductCodes ;
    }
    
    private Dictionary<string, int> GetPickUpNumberForConduitsToPullBox( Document document, List<PickUpModel> pickUpModelsByLevel )
    {
      var result = new Dictionary<string, int>() ;
      if ( pickUpModelsByLevel.All( x => string.IsNullOrEmpty( x.PickUpNumber ) ) ) return result ;
      var pullBoxIdWithPickUpNumbers = new Dictionary<string, int>() ;
      var routeCache = RouteCache.Get( DocumentKey.Get( document ) ) ;
      var pickUpNumberOfPullBox = pickUpModelsByLevel.Where( x => !string.IsNullOrEmpty( x.PickUpNumber ) ).Max( x => Convert.ToInt32( x.PickUpNumber ) ) ;
      var routes = pickUpModelsByLevel.Select( x => x.RouteName ).Where( r => r != "" ).Distinct() ;
      foreach ( var route in routes ) {
        var conduitPickUpModel = pickUpModelsByLevel
          .Where( p => p.RouteName == route && p.EquipmentType == PickUpViewModel.ProductType.Conduit.GetFieldName() )
          .GroupBy( x => x.ProductCode, ( key, p ) => new { ProductCode = key, PickUpModels = p.ToList() } )
          .FirstOrDefault() ;
        if ( conduitPickUpModel == null ) continue ;

        var pickUpModelsGroupsByRouteNameRef = conduitPickUpModel.PickUpModels.GroupBy( p => p.RelatedRouteName ) ;
        foreach ( var pickUpModelsGroup in pickUpModelsGroupsByRouteNameRef ) {
          var routeName = pickUpModelsGroup.Key ;
          var lastRoute = routeCache.LastOrDefault( r => r.Key == routeName ) ;
          var lastSegment = lastRoute.Value.RouteSegments.Last() ;
          var pullBoxUniqueId = IsSegmentConnectedToPoPullBox( document, lastSegment ) ;
          if ( string.IsNullOrEmpty( pullBoxUniqueId ) ) continue ;

          if ( pullBoxIdWithPickUpNumbers.ContainsKey( pullBoxUniqueId ) )
            result.Add( routeName, pullBoxIdWithPickUpNumbers[pullBoxUniqueId] );
          else {
            pickUpNumberOfPullBox++ ;
            pullBoxIdWithPickUpNumbers.Add( pullBoxUniqueId, pickUpNumberOfPullBox );
            result.Add( routeName, pickUpNumberOfPullBox );
          }
        }
      }

      return result; 
    }
    
    private string IsSegmentConnectedToPoPullBox( Document document, RouteSegment lastSegment )
    {
      var pullBoxUniqueId = string.Empty ;
      var toEndPointKey = lastSegment.ToEndPoint.Key ;
      var toElementId = toEndPointKey.GetElementUniqueId() ;
      if ( string.IsNullOrEmpty( toElementId ) ) 
        return pullBoxUniqueId ;
      var toConnector = document.GetAllElements<FamilyInstance>().OfCategory( BuiltInCategory.OST_ElectricalFixtures )
        .FirstOrDefault( c => c.UniqueId == toElementId ) ;
      if ( toConnector != null && toConnector.GetConnectorFamilyType() == ConnectorFamilyType.PullBox )
        pullBoxUniqueId = toConnector.UniqueId ;
      return pullBoxUniqueId ;
    }
    
    private int GetRowOfLevel( List<PickUpModel> pickUpModels, ISheet sheet, int rowStart, IReadOnlyDictionary<string, int> pickUpNumberForConduitsToPullBox )
    {
      if ( ! pickUpModels.Any() ) return rowStart ;
      var pickUpNumbers = GetPickUpNumbersList( pickUpModels ) ;
      var pickUpModel = pickUpModels.First() ;
      var row = sheet.GetRow( rowStart ) ;
      var isTani = IsTani( pickUpModel ) ;
      double total = 0 ;
      Dictionary<string, int> trajectory = new Dictionary<string, int>() ;
      var routes = RouteCache.Get( DocumentKey.Get( _document ) ) ;
      var inforDisplays = GetInforDisplays( pickUpModels, routes ) ;
      var isMoreTwoWireBook = IsMoreTwoWireBook( pickUpModels ) ;
      foreach ( var pickUpNumber in pickUpNumbers ) {
        string stringNotTani = string.Empty ;
        Dictionary<string, double> notSeenQuantities = new Dictionary<string, double>() ;
        Dictionary<string, double> notSeenQuantitiesPullBox = new Dictionary<string, double>() ;
        var items = pickUpModels.Where( p => p.PickUpNumber == pickUpNumber ).ToList() ;
        var itemFirst = items.First() ;
        var wireBook = ( string.IsNullOrEmpty( itemFirst.WireBook ) || itemFirst.WireBook == "1" ) ? string.Empty : itemFirst.WireBook ;
        var itemsGroupByRoute = items.Where( item => ! string.IsNullOrEmpty( item.Quantity ) ).GroupBy( i => i.RelatedRouteName ) ;
        var listSeenQuantity = new List<double>() ;
        var listSeenQuantityPullBox = new List<string>() ;
        var valueDetailTableStr = string.Empty ;
        double totalBasedOnCreateTable = 0 ;
        foreach ( var itemGroupByRoute in itemsGroupByRoute ) {
          valueDetailTableStr = wireBook ;
          double seenQuantity = 0 ;

          var lastSegment = GetLastSegment( itemGroupByRoute.Key, routes ) ;
          var isSegmentConnectedToPullBox = IsSegmentConnectedToPullBox( lastSegment ) ;
          var isSegmentFromPowerToPullBox = IsSegmentFromPowerToPullBox( lastSegment ) ;
          var pickUpNumberForConduitToPullBox = pickUpNumberForConduitsToPullBox.SingleOrDefault( p => p.Key == itemGroupByRoute.Key ) ;
          var pickUpNumberPullBox = pickUpNumberForConduitToPullBox.Value ;

          foreach ( var item in itemGroupByRoute ) {
            double.TryParse( item.Quantity, out var quantity ) ;
            if ( ! string.IsNullOrEmpty( item.Direction ) ) {
              if ( isSegmentFromPowerToPullBox ) {
                if ( ! notSeenQuantitiesPullBox.Keys.Contains( item.Direction ) ) {
                  notSeenQuantitiesPullBox.Add( item.Direction, 0 ) ;
                }

                notSeenQuantitiesPullBox[ item.Direction ] += quantity ;
              }
              else {
                if ( ! notSeenQuantities.Keys.Contains( item.Direction ) ) {
                  notSeenQuantities.Add( item.Direction, 0 ) ;
                }

                notSeenQuantities[ item.Direction ] += quantity ;
              }
            }
            else {
              if ( ! isTani ) stringNotTani += string.IsNullOrEmpty( stringNotTani ) ? item.SumQuantity : $"＋{item.SumQuantity}" ;
              seenQuantity += quantity ;
            }

            totalBasedOnCreateTable += quantity ;
          }
          
          if ( seenQuantity > 0 ) {
            if ( isSegmentConnectedToPullBox ) {
              var countStr = string.Empty ;
              var inforDisplay = inforDisplays.SingleOrDefault( x => x.RouteNameRef == itemGroupByRoute.Key ) ;
              var numberPullBox = DoconTypes.First().TheValue ? $"[{(pickUpNumberPullBox)}]" : string.Empty;
              if ( inforDisplay != null && inforDisplay.IsDisplay) {
                countStr = ( inforDisplay.NumberDisplay == 1 ? string.Empty : $"×{inforDisplay.NumberDisplay}" ) + ( isMoreTwoWireBook ? $"×N" : string.IsNullOrEmpty(valueDetailTableStr) ? string.Empty: $"×{valueDetailTableStr}" ) ;
                inforDisplay.IsDisplay = false ;
                if ( isSegmentFromPowerToPullBox ) {
                  listSeenQuantityPullBox.Add(numberPullBox + $"({Math.Round( seenQuantity, isTani ? 1 : 2 ).ToString( CultureInfo.InvariantCulture )}＋↓{Math.Round( notSeenQuantitiesPullBox.First().Value, isTani ? 1 : 2 )})" + countStr);
                }
                else {
                  listSeenQuantityPullBox.Add( numberPullBox + Math.Round( seenQuantity, isTani ? 1 : 2 ).ToString( CultureInfo.InvariantCulture ) + countStr);
                }
              }
            }
            else {
              listSeenQuantity.Add( Math.Round( seenQuantity, isTani ? 1 : 2 ) ) ;
            }
          }
        }

        total += ! string.IsNullOrEmpty( wireBook ) ? Math.Round( totalBasedOnCreateTable, isTani ? 1 : 2 ) * int.Parse( wireBook ) : Math.Round( totalBasedOnCreateTable, isTani ? 1 : 2 ) ;

        var number = DoconTypes.First().TheValue && ! string.IsNullOrEmpty( pickUpNumber ) ? "[" + pickUpNumber + "]" : string.Empty ;
        var seenQuantityStr = isTani ? string.Join( "＋", listSeenQuantity ) : string.Join("＋",stringNotTani.Split( '+' )) ;

        var seenQuantityPullBoxStr = string.Empty ;
        if ( listSeenQuantityPullBox.Any() ) {
          seenQuantityPullBoxStr = string.Join($"＋", listSeenQuantityPullBox ) ;
        }
        
        var notSeenQuantityStr = string.Empty ;
        foreach ( var (_, value) in notSeenQuantities ) {
          notSeenQuantityStr += value > 0 ? "＋↓" + Math.Round( value, isTani ? 1 : 2 ) : string.Empty ;
        }

        var key = isTani ? ( "(" + seenQuantityStr + notSeenQuantityStr + ")" ) + (string.IsNullOrEmpty( valueDetailTableStr ) ? string.Empty : $"×{valueDetailTableStr}") + (string.IsNullOrEmpty( seenQuantityPullBoxStr ) ? string.Empty : $"＋{seenQuantityPullBoxStr}" )  : ( seenQuantityStr + notSeenQuantityStr ) ;
        var itemKey = trajectory.FirstOrDefault( t => t.Key.Contains( key ) ).Key ;

        if ( string.IsNullOrEmpty( itemKey ) )
          trajectory.Add( number + key, 1 ) ;
        else {
          trajectory[ itemKey ]++ ;
        }
      }
      
      List<string> trajectoryStr = ( from item in trajectory select item.Value == 1 ? item.Key : item.Key + "×" + item.Value ).ToList() ;
      int lengthOfCellMerge = GetWidthOfCellMerge( sheet, 5, 15 ) ;
      
      var valueOfCell = string.Empty ;
      var trajectoryStrCount = trajectoryStr.Count ;
      var count = 0 ;
      if ( trajectoryStrCount > 1 ) {
        for ( var i = 0 ; i < trajectoryStrCount ; i++ ) {
          valueOfCell += trajectoryStr[ i ] + (i == trajectoryStrCount - 1 ? "": "＋");
          if ( valueOfCell.Length * 2.5  < lengthOfCellMerge/256.0 && i < trajectoryStrCount - 1 ) continue;
          if ( count == 0 ) {
            count++ ;
          }
          else {
            rowStart++ ;
          }
        }
      }
      
      rowStart++ ;
      return rowStart ;
    }
    
    public class ListBoxItem
    {
      public string? TheText { get ; set ; }
      public bool TheValue { get ; set ; }
    }

    private class InforDisplay
    {
      public bool IsDisplay { get ; set ; }
      public string? IdPullBox { get ; set ; }
      public int NumberDisplay { get ; set ; }
      public string? RouteNameRef { get ; set ; }

      public InforDisplay( bool isDisplay, string? idPullBox, int numberDisplay, string? routeNameRef )
      {
        IsDisplay = isDisplay ;
        IdPullBox = idPullBox??string.Empty ;
        NumberDisplay = numberDisplay ;
        RouteNameRef = routeNameRef??string.Empty ; ;
      }
    }
  }
}