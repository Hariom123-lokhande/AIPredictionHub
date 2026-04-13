namespace AIPredictionHub.Models.Laptop
{
    public static class LaptopDummyData
    {
        public static List<LaptopData> GetRecords() => new()
        {
            new LaptopData { Brand = "Dell",    RAM = 8,  Storage = 512,  Processor = "Intel Core i5",  ScreenSize = 15.6f, Price = 55000 },
            new LaptopData { Brand = "Dell",    RAM = 16, Storage = 512,  Processor = "Intel Core i7",  ScreenSize = 15.6f, Price = 75000 },
            new LaptopData { Brand = "HP",      RAM = 8,  Storage = 256,  Processor = "Intel Core i5",  ScreenSize = 14.0f, Price = 48000 },
            new LaptopData { Brand = "HP",      RAM = 16, Storage = 1000, Processor = "Intel Core i7",  ScreenSize = 15.6f, Price = 82000 },
            new LaptopData { Brand = "Lenovo",  RAM = 8,  Storage = 512,  Processor = "AMD Ryzen 5",    ScreenSize = 14.0f, Price = 50000 },
            new LaptopData { Brand = "Lenovo",  RAM = 16, Storage = 512,  Processor = "AMD Ryzen 7",    ScreenSize = 15.6f, Price = 70000 },
            new LaptopData { Brand = "Apple",   RAM = 8,  Storage = 256,  Processor = "Apple M1",       ScreenSize = 13.3f, Price = 99000 },
            new LaptopData { Brand = "Apple",   RAM = 16, Storage = 512,  Processor = "Apple M2",       ScreenSize = 14.0f, Price = 125000 },
            new LaptopData { Brand = "Asus",    RAM = 8,  Storage = 512,  Processor = "Intel Core i5",  ScreenSize = 15.6f, Price = 52000 },
            new LaptopData { Brand = "Asus",    RAM = 16, Storage = 1000, Processor = "AMD Ryzen 7",    ScreenSize = 15.6f, Price = 72000 },
            new LaptopData { Brand = "Acer",    RAM = 4,  Storage = 256,  Processor = "Intel Core i3",  ScreenSize = 15.6f, Price = 35000 },
        };
    }
}
