public class House
{
    public int Id { get; set; } // Thêm dòng này để làm Khóa chính (Primary Key) trong SQL
    public int BuildingType { get; set; }
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float RotY { get; set; }
    public string Timestamp { get; set; }
}