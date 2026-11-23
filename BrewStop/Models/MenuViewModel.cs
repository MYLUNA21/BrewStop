using System.Globalization;

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
        public string Group { get; set; } = "Drinks";
        public string ImageUrl { get; set; } = string.Empty;
        public string BadgeText { get; set; } = "Bestseller";
        public int Stock { get; set; }
        public bool IsAvailable => Stock > 0;

        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
        public double Popularity { get; set; }
        public bool IsFavorite { get; set; }
        public string PairingNote { get; set; } = string.Empty;
        public string RatingText => AverageRating.ToString("0.0", CultureInfo.InvariantCulture);
    }

    public class CategoryGroupDto
    {
        public string Slug { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Group { get; set; } = "Drinks";
        public string ImageUrl { get; set; } = string.Empty;
        public List<MenuItemDto> Items { get; set; } = new();
    }

    public class SizeOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public class AddOnOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public class MenuViewModel
    {
        public List<CategoryGroupDto> Categories { get; set; } = new();
        public List<CategoryGroupDto> Pills { get; set; } = new();
        public string ActiveCategory { get; set; } = "drinks";
        public string ActiveGroup { get; set; } = "Drinks";
        public string Sort { get; set; } = "popularity";
        public string? SearchTerm { get; set; }

        public List<MenuItemDto> Drinks { get; set; } = new();
        public List<MenuItemDto> Pastries { get; set; } = new();

        public string HeroImageUrl { get; set; } = string.Empty;
        public string HeroImageAlt { get; set; } = string.Empty;

        public List<SizeOptionDto> Sizes { get; set; } = new();
        public List<AddOnOptionDto> AddOns { get; set; } = new();

        public int ResultCount => Categories.Sum(c => c.Items.Count);
    }
}