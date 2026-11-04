namespace BrewStop.Models
{
    public class MenuItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string CategorySlug { get; set; } = string.Empty; 
        public string CategoryName { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string BadgeText { get; set; } = "Bestseller";
    }

    public class CategoryGroupDto
    {
        public string Slug { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Group { get; set; } = "Drinks"; 
        public string ImageUrl { get; set; } = string.Empty;
        public List<MenuItemDto> Items { get; set; } = new();
    }

    public class MenuViewModel
    {
        public List<CategoryGroupDto> Categories { get; set; } = new();
    }
}