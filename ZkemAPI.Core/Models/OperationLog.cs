using System;

public class OperationLog
{
    public int Index { get; set; }
    public int AdminId { get; set; }         // ID administratora wykonującego operację
    public int Operation { get; set; }        // Kod operacji
    public string OperationName { get; set; } // Nazwa operacji w języku polskim
    public DateTime DateTime { get; set; }
    public string WorkCode { get; set; }      // Dodatkowe informacje o operacji
} 