﻿using System.Linq ;
using System.Windows.Forms ;
using Arent3d.Architecture.Routing.AppBase.Forms ;
using Arent3d.Architecture.Routing.AppBase.Manager ;
using Arent3d.Architecture.Routing.AppBase.ViewModel ;
using Arent3d.Architecture.Routing.Extensions ;
using Arent3d.Revit.I18n ;
using Arent3d.Revit.UI ;
using Autodesk.Revit.DB ;
using Autodesk.Revit.UI ;

namespace Arent3d.Architecture.Routing.AppBase.Commands.Initialization
{
  public abstract class PickUpNumberSettingCommandBase : IExternalCommand
  {
    public Result Execute( ExternalCommandData commandData, ref string message, ElementSet elements )
    {
      var document = commandData.Application.ActiveUIDocument.Document ;

      var viewModel = new PickUpNumberSettingViewModel( document ) ;
      var dialog = new PickUpNumberSettingDialog( viewModel ) ;

      dialog.ShowDialog() ;
      return dialog.DialogResult == false
        ? Result.Cancelled
        : document.Transaction(
          "TransactionName.Commands.Initialization.PickUpNumberSetting".GetAppStringByKeyOrDefault(
            "Pick Up Number Setting" ), _ =>
          {
            var level = document.ActiveView.GenLevel ;
            var textNotePickUpStorable = document.GetTextNotePickUpStorable() ;
            var isDisplay = textNotePickUpStorable.TextNotePickUpData.Any( t => t.Level == level.Name ) ;

            if ( !isDisplay ) return Result.Succeeded ;
            
            var pickUpViewModel = new PickUpViewModel( document ) ;
            if ( ! pickUpViewModel.DataPickUpModels.Any() ) return Result.Succeeded ;
            
            var pickUpModels = pickUpViewModel.DataPickUpModels.Where( p => p.Floor == level.Name ).ToList() ;
            if ( !pickUpModels.Any() ) {
              MessageBox.Show( "Don't have pick up data on this view.", "Message Warning" ) ;
              return Result.Succeeded ;
            }
            PickUpManager.RemoveTextNotePickUp( document, level.Name ) ;
            PickUpManager.ShowTextNotePickUp( textNotePickUpStorable, document, level, pickUpModels ) ;

            return Result.Succeeded ;
          } ) ;
    }
  }
}