using LedgerCore.Enums;

namespace LedgerCore.Models;

public sealed record AuthorizationView(string AuthId, string AccountId, AuthorizationStatus Status, decimal Requested, decimal ActiveHold);
