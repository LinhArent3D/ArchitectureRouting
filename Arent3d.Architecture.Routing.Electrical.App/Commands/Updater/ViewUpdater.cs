﻿using System;
using System.Collections.Generic;
using System.Linq;
using Arent3d.Revit;
using Autodesk.Revit.DB;

namespace Arent3d.Architecture.Routing.Electrical.App.Commands.Updater
{
public class ViewUpdater : IUpdater
  {
    private static UpdaterId? _updaterId ;

    public ViewUpdater( AddInId? id )
    {
      _updaterId = new UpdaterId( id, new Guid( "710A4FA7-D660-40EA-AC83-505D0A10199C" ) ) ;
    }

    public void Execute( UpdaterData data )
    {
      var document = data.GetDocument();
      if(document is null) return;
      var listLines = TextNoteArent.StorageLines.Where(x=>TextNoteArent.CheckIdIsDeleted(document, x.Key))
        .SelectMany(x => x.Value).ToList();
      document.Delete(listLines.Where(x=>TextNoteArent.CheckIdIsDeleted(document, x)).ToList());

      TextNoteArent.StorageLines = new Dictionary<ElementId, List<ElementId>>();

      var allText = new FilteredElementCollector(document, document.ActiveView.Id).WhereElementIsNotElementType().OfClass(typeof(TextNote)).Cast<TextNote>();
      var filterText = allText.Where(t => t.TextNoteType.Name == TextNoteArent.ArentTextNoteType).ToList();
      filterText.ForEach(TextNoteArent.CreateSingleBoxText);
    }

  
    public UpdaterId? GetUpdaterId()
    {
      return _updaterId ;
    }

    public ChangePriority GetChangePriority()
    {
      return ChangePriority.Annotations ;
    }

    public string GetUpdaterName()
    {
      return "ViewUpdater" ;
    }

    public string GetAdditionalInformation()
    {
      return "Arent, " + "https://arent3d.com" ;
    }
