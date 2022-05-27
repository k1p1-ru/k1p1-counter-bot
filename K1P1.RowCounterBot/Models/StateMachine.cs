namespace K1P1.RowCounterBot.Models;

/// <summary>
/// To await a user's second action: to enter a name of a timer or to confirm a archiving
/// </summary>
public class StateMachine
{
    public long ChatId { get; private set; }
    
    public States State { get; private set; }

    public StateMachine(long chatId, States state)
    {
        ChatId = chatId;
        State = state;
    }
}

public enum States
{
    Adding = 1,
    
    Archiving = 2,
    
    Unarchiving = 3
}