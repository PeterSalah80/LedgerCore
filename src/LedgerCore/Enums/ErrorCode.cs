namespace LedgerCore.Enums;

public enum ErrorCode
{
    DuplicateEventId,
    UnknownAccount,
    NotACustomerAccount,
    CurrencyMismatch,
    MissingAmount,
    NonPositiveAmount,
    InvalidPrecision,
    InvalidInstalments,
    OutsideWindow,
    InvalidValueDate,
    FutureValueDate,
    LateEvent,
    MissingAuthId,
    DuplicateAuthorization,
    UnmatchedSettlement,
    SettlementOnDeclinedAuth,
    SettlementOnSettledAuth,
    SettlementExceedsHold,
    AuthAccountMismatch,
    ReversalTargetNotFound,
    ReversalAccountMismatch,
    ReversalTargetNotReversible,
    ReversalTargetNotPosted,
    AlreadyReversed,
    ReversalAmountMismatch,
    NoFeeScheduleForCurrency
}
