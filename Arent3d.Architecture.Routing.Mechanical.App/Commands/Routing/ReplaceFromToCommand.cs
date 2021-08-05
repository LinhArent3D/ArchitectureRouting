﻿using Arent3d.Architecture.Routing.AppBase.Commands.Routing ;
using Arent3d.Revit.UI ;
using Autodesk.Revit.Attributes ;

namespace Arent3d.Architecture.Routing.Mechanical.App.Commands.Routing
{
  [Transaction( TransactionMode.Manual )]
  [DisplayNameKey( "Mechanical.App.Commands.Routing.ReplaceFromToCommand", DefaultString = "Replace\nFrom-To" )]
  [Image( "resources/ReplaceFromTo.png" )]
  public class ReplaceFromToCommand : ReplaceFromToCommandBase
  {
    protected override string GetTransactionNameKey() => "TransactionName.Commands.Routing.ReplaceFromTo" ;

    protected override AddInType GetAddInType()
    {
      return AddInType.Mechanical ;
    }
  }
}