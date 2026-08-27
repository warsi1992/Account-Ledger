using Ledger.Core;

var ledger = new AccountLedger();
ledger.Replay();

foreach (var line in ledger.DailyReport())
    Console.WriteLine(line);
