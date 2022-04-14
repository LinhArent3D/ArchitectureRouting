﻿using System.Linq ;
using Arent3d.Revit;
using Arent3d.Revit.UI ;
using Arent3d.Utility ;
using Autodesk.Revit.Attributes ;
using Autodesk.Revit.DB ;
using Autodesk.Revit.UI ;

namespace Arent3d.Architecture.Routing.Electrical.App.Commands.Annotation
{
  [Transaction( TransactionMode.Manual )]
  [DisplayNameKey( "Electrical.App.Commands.Annotation.SimpleBorderCommand", DefaultString = "Simple Border" )]
  [Image( "resources/Initialize-32.bmp", ImageType = Revit.UI.ImageType.Large )]
  public class SimpleBorderCommand : IExternalCommand
  {
    public const string TextNoteTypeName = "ARENT_2.5MM_SIMPLE-BORDER" ;

    public Result Execute( ExternalCommandData commandData, ref string message, ElementSet elements )
    {
      var application = commandData.Application ;
      var document = application.ActiveUIDocument.Document ;

      using var transaction = new Transaction( document ) ;
      transaction.Start( "Simple TextNote Border" ) ;
      
      var textNoteType = FindOrCreateTextNoteType( document ) ;
      if ( null == textNoteType ) {
        message = "Cannot create text note type!" ;
        return Result.Failed ;
      }

      if(document.IsDefaultElementTypeIdValid(ElementTypeGroup.TextNoteType, textNoteType.Id))
        document.SetDefaultElementTypeId(ElementTypeGroup.TextNoteType, textNoteType.Id);

      transaction.Commit() ;
      
      var textCommandId = RevitCommandId.LookupPostableCommandId(PostableCommand.Text);
      if(application.CanPostCommand(textCommandId))
        application.PostCommand(textCommandId);
      
      return Result.Succeeded ;
    }
    private static TextNoteType? FindOrCreateTextNoteType(Document document)
    {
      var textNoteTypes = new FilteredElementCollector( document ).OfClass( typeof( TextNoteType ) ).OfType<TextNoteType>().EnumerateAll() ;
      if ( ! textNoteTypes.Any() )
        return null ;
      
      var textNoteType = textNoteTypes.SingleOrDefault( x => x.Name == TextNoteTypeName ) ;
      if ( null != textNoteType ) 
        return textNoteType ;
      
      textNoteType = textNoteTypes.First().Duplicate(TextNoteTypeName) as TextNoteType;
      if ( null == textNoteType )
        return null ;
      
      textNoteType.get_Parameter( BuiltInParameter.TEXT_BOX_VISIBILITY ).Set( 1 ) ;
      textNoteType.get_Parameter( BuiltInParameter.TEXT_SIZE ).Set( 2.5.MillimetersToRevitUnits() ) ;

      return textNoteType ;
    }
  }
  
}