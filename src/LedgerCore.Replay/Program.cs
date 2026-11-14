using LedgerCore;
using LedgerCore.Replay;
using LedgerCore.Scenario;

var ledger = new Ledger(ScenarioData.Accounts(), ScenarioData.Policy);
foreach (var ev in ScenarioData.Events())
{
    ledger.Submit(ev);
}
ledger.Finish();
new ReportPrinter(ledger, Console.Out).PrintAll();
