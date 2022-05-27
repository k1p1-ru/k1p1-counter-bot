namespace K1P1.RowCounterBot.Models;

public class Counter
{
    public int Id { get; private set; }
    
    public long ChatId { get; private set; }

    public string Name { get; private set; }
    
    public uint RowCount { get; private set; }
    
    public bool Archived { get; private set; }

    public Counter(int id, long chatId, string name)
    {
        Id = id;
        ChatId = chatId;
        Name = name;
        RowCount = 0;
        Archived = false;
    }

    public void Increase()
    {
        RowCount++;
    }

    public void Decrease()
    {
        if(RowCount > 0)
            RowCount--;
    }
    
    public void Archive()
    {
        Archived = true;
    }
}