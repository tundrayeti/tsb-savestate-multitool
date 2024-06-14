using TSB; // aka TSB_SaveState_MultiTool
using NLog;

Logger log = LogManager.GetCurrentClassLogger();
log.Trace("Hello, World!");

/// TSB RosterRipper ///
// ROM (.nes)
string romFileName = @"E:\Media\ROMs\NES\TPC_TSB_tapmeter.nes";
//TSB_RosterRipper rosterRipper = new(romFileName);
//rosterRipper.Rip();

/// TSB StatExtractor ///
// Save state file (e.g. .ns1)
string saveStateFileName = @"D:\Program Files\anticheat V2\states\TPC_TSB_tapmeter.ns1"; 
TSB_StatExtractor statExtractor = new(romFileName);
statExtractor.ExportStats(saveStateFileName);

/// TSB ConditionChecker ///
TSB_ConditionChecker conditionChecker = new(romFileName);
//conditionChecker.Start(saveStateFileName);

log.Trace("El fin.");
// end
/////////