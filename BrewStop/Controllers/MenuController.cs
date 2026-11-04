using BrewStop.Models;
using Microsoft.AspNetCore.Mvc;

namespace BrewStop.Controllers
{
    public class MenuController : Controller
    {
        public IActionResult Index(string? category)
        {
            var menuData = GetSampleMenuData();
            ViewBag.ActiveCategory = category ?? "all";
            return View(menuData);
        }

        private MenuViewModel GetSampleMenuData()
        {
            return new MenuViewModel
            {
                Categories = new List<CategoryGroupDto>
                {
                    // 1. FEATURED DRINKS (5 Unique Items)
                    new CategoryGroupDto
                    {
                        Slug = "featured-drinks",
                        Name = "Featured Drinks",
                        Group = "Drinks",
                        ImageUrl = "https://images.unsplash.com/photo-1485808191679-5f86510681a2?auto=format&fit=crop&w=500&q=80",
                        Items = new List<MenuItemDto>
                        {
                            new MenuItemDto { Id = 1, Name = "Caramel Macchiato", Price = 140.00m, Description = "Freshly steamed milk with vanilla syrup, marked with espresso and topped with rich caramel drizzle.", CategorySlug = "featured-drinks", CategoryName = "Featured Drinks", ImageUrl = "https://images.unsplash.com/photo-1485808191679-5f86510681a2?auto=format&fit=crop&w=500&q=80", BadgeText = "Campus Fav" },
                            new MenuItemDto { Id = 2, Name = "Velvet Cold Foam Nitro", Price = 160.00m, Description = "Bold nitrogen-infused cold brew cascading with a velvety layer of vanilla sweet cold foam cream.", CategorySlug = "featured-drinks", CategoryName = "Featured Drinks", ImageUrl = "https://images.unsplash.com/photo-1517701604599-bb29b565090c?auto=format&fit=crop&w=500&q=80", BadgeText = "Signature" },
                            new MenuItemDto { Id = 3, Name = "Iced Salted Caramel Mocha", Price = 155.00m, Description = "Espresso combined with rich mocha sauce and toffee nut syrup, finished with sea salt cold foam.", CategorySlug = "featured-drinks", CategoryName = "Featured Drinks", ImageUrl = "https://images.unsplash.com/photo-1541167760496-1628856ab772?auto=format&fit=crop&w=500&q=80", BadgeText = "Popular" },
                            new MenuItemDto { Id = 4, Name = "Spanish Latte Cold Brew", Price = 145.00m, Description = "Concentrated espresso poured over sweetened condensed milk and chilled whole milk.", CategorySlug = "featured-drinks", CategoryName = "Featured Drinks", ImageUrl = "https://images.unsplash.com/photo-1551024709-8f23befc6f87?auto=format&fit=crop&w=500&q=80", BadgeText = "Bestseller" },
                            new MenuItemDto { Id = 5, Name = "Brown Sugar Oatmilk Shaken Espresso", Price = 165.00m, Description = "Blonde espresso shaken with brown sugar and cinnamon, topped off with creamy oatmilk.", CategorySlug = "featured-drinks", CategoryName = "Featured Drinks", ImageUrl = "https://images.unsplash.com/photo-1461023058943-07fcbe16d735?auto=format&fit=crop&w=500&q=80", BadgeText = "New" }
                        }
                    },

                    // 2. BREWED COFFEE (5 Unique Items)

                    new CategoryGroupDto
                    {
                        Slug = "brewed-coffee",
                        Name = "Brewed Coffee",
                        Group = "Drinks",
                        ImageUrl = "https://images.unsplash.com/photo-1501339847302-ac426a4a7cbb?auto=format&fit=crop&w=500&q=80",
                        Items = new List<MenuItemDto>
                        {
                            
                            new MenuItemDto { Id = 6, Name = "Signature Cold Brew Coffee", Price = 125.00m, Description = "Slow-steeped in cool water for 20 hours to extract a smooth, naturally sweet coffee taste.", CategorySlug = "brewed-coffee", CategoryName = "Brewed Coffee", ImageUrl = "https://images.unsplash.com/photo-1495474472287-4d71bcdd2085?auto=format&fit=crop&w=500&q=80", BadgeText = "Cold" },
                            new MenuItemDto { Id = 7, Name = "Vanilla Sweet Cream Cold Brew", Price = 140.00m, Description = "Brewed cold coffee accented with vanilla syrup and topped with house-made sweet cream.", CategorySlug = "brewed-coffee", CategoryName = "Brewed Coffee", ImageUrl = "https://images.unsplash.com/photo-1517256064527-09c73fc73e38?auto=format&fit=crop&w=500&q=80", BadgeText = "Popular" },
                            new MenuItemDto { Id = 8, Name = "Blonde Light Roast Brew", Price = 110.00m, Description = "Lightly roasted coffee beans offering citrus notes with a soft, sweet cup finish.", CategorySlug = "brewed-coffee", CategoryName = "Brewed Coffee", ImageUrl = "https://images.unsplash.com/photo-1442512595331-e89e73853f31?auto=format&fit=crop&w=500&q=80", BadgeText = "Mellow" }
                        }
                    },

                    // 3. ESPRESSO (5 Unique Items)
                    new CategoryGroupDto
                    {
                        Slug = "espresso",
                        Name = "Espresso",
                        Group = "Drinks",
                        ImageUrl = "https://images.unsplash.com/photo-1570968915860-54d5c301fa9f?auto=format&fit=crop&w=500&q=80",
                        Items = new List<MenuItemDto>
                        {
                            new MenuItemDto { Id = 9, Name = "Caffè Americano", Price = 115.00m, Description = "Rich espresso shots topped with hot water create a deep layer of crema.", CategorySlug = "espresso", CategoryName = "Espresso", ImageUrl = "https://images.unsplash.com/photo-1570968915860-54d5c301fa9f?auto=format&fit=crop&w=500&q=80", BadgeText = "Hot/Iced" },
                            new MenuItemDto { Id = 10, Name = "Classic Cappuccino", Price = 130.00m, Description = "Dark espresso lying under a smooth, thick cushion of micro-foamed milk.", CategorySlug = "espresso", CategoryName = "Espresso", ImageUrl = "https://images.unsplash.com/photo-1578314675249-a6910f80cc4e?auto=format&fit=crop&w=500&q=80", BadgeText = "Barista Fav" },
                            new MenuItemDto { Id = 11, Name = "Caffè Latte", Price = 130.00m, Description = "Dark roast espresso balanced with steamed milk and a thin layer of foam.", CategorySlug = "espresso", CategoryName = "Espresso", ImageUrl = "https://images.unsplash.com/photo-1577968897966-3d4325b36b61?auto=format&fit=crop&w=500&q=80", BadgeText = "Smooth" },
                            new MenuItemDto { Id = 12, Name = "Caffè Mocha", Price = 145.00m, Description = "Espresso combined with bittersweet mocha sauce and steamed milk, topped with whipped cream.", CategorySlug = "espresso", CategoryName = "Espresso", ImageUrl = "https://images.unsplash.com/photo-1530373239216-42518e6b4063?auto=format&fit=crop&w=500&q=80", BadgeText = "Chocolate" },
                            new MenuItemDto { Id = 13, Name = "Flat White", Price = 135.00m, Description = "Ristretto shots of espresso infused with steamed whole milk for a velvety finish.", CategorySlug = "espresso", CategoryName = "Espresso", ImageUrl = "https://images.unsplash.com/photo-1585494156145-1c60a4fe952b?auto=format&fit=crop&w=500&q=80", BadgeText = "Rich" }
                        }
                    },

                    // 4. BLENDED BEVERAGES (5 Unique Items)
                    new CategoryGroupDto
                    {
                        Slug = "blended-beverage",
                        Name = "Blended Beverage",
                        Group = "Drinks",
                        ImageUrl = "https://images.unsplash.com/photo-1536256263959-770b48d82b0a?auto=format&fit=crop&w=500&q=80",
                        Items = new List<MenuItemDto>
                        {
                            new MenuItemDto { Id = 14, Name = "Caramel Coffee Frappuccino", Price = 165.00m, Description = "Coffee blended with caramel syrup, milk, and ice, topped with whipped cream and caramel drizzle.", CategorySlug = "blended-beverage", CategoryName = "Blended Beverage", ImageUrl = "https://www.torani.com/media/catalog/product/S/u/Sugar_Free_Classic_Caramel_Cappuccino.jpg?optimize=medium&fit=bounds&height=815&width=650&canvas=650:815", BadgeText = "Top Blended" },
                            new MenuItemDto { Id = 15, Name = "Java Chip Frappuccino", Price = 175.00m, Description = "Roast coffee, mocha sauce, and chocolate chips blended with ice and topped with whipped cream.", CategorySlug = "blended-beverage", CategoryName = "Blended Beverage", ImageUrl = "https://images.unsplash.com/photo-1546833999-b9f581a1996d?auto=format&fit=crop&w=500&q=80", BadgeText = "Choco Crunch" },
                            new MenuItemDto { Id = 16, Name = "Matcha Green Tea Blended", Price = 170.00m, Description = "Premium Japanese green tea matcha blended with ice, milk, and sweet cream.", CategorySlug = "blended-beverage", CategoryName = "Blended Beverage", ImageUrl = "https://images.unsplash.com/photo-1576092768241-dec231879fc3?auto=format&fit=crop&w=500&q=80", BadgeText = "Matcha" },
                            new MenuItemDto { Id = 17, Name = "Double Chocolaty Chip Crème", Price = 170.00m, Description = "Rich mocha-flavored sauce and chocolate chips blended with milk and ice (Coffee-free).", CategorySlug = "blended-beverage", CategoryName = "Blended Beverage", ImageUrl = "https://images.unsplash.com/photo-1572490122747-3968b75cc699?auto=format&fit=crop&w=500&q=80", BadgeText = "Kid Friendly" },
                            new MenuItemDto { Id = 18, Name = "Strawberries & Crème Blended", Price = 165.00m, Description = "Blended ice, milk, and sweet strawberry puree layered with whipped cream.", CategorySlug = "blended-beverage", CategoryName = "Blended Beverage", ImageUrl = "https://images.unsplash.com/photo-1553787499-6f9133860278?auto=format&fit=crop&w=500&q=80", BadgeText = "Fruity" }
                        }
                    },

                    // 5. TEAVANA TEA 
                    new CategoryGroupDto
                    {
                        Slug = "teavana-tea",
                        Name = "Teavana Tea",
                        Group = "Drinks",
                        ImageUrl = "https://images.unsplash.com/photo-1556679343-c7306c1976bc?auto=format&fit=crop&w=500&q=80",
                        Items = new List<MenuItemDto>
                        {
                            new MenuItemDto { Id = 19, Name = "Iced Green Tea Latte", Price = 145.00m, Description = "Smooth Japanese matcha tea sweetened and mixed with chilled milk.", CategorySlug = "teavana-tea", CategoryName = "Teavana Tea", ImageUrl = "https://images.unsplash.com/photo-1556679343-c7306c1976bc?auto=format&fit=crop&w=500&q=80", BadgeText = "Matcha Fav" },
                            new MenuItemDto { Id = 20, Name = "Emperor’s Clouds & Mist", Price = 115.00m, Description = "Green tea harvested at high altitudes with delicate floral notes and sweet finish.", CategorySlug = "teavana-tea", CategoryName = "Teavana Tea", ImageUrl = "https://images.unsplash.com/photo-1627435601361-ec25f5b1d0e5?auto=format&fit=crop&w=500&q=80", BadgeText = "Hot Tea" },
                            new MenuItemDto { Id = 21, Name = "Iced Peach Black Tea", Price = 135.00m, Description = "Bold black tea shaken with sweet peach juice blend and ice cubes.", CategorySlug = "teavana-tea", CategoryName = "Teavana Tea", ImageUrl = "https://images.unsplash.com/photo-1597481499750-3e6b22637e12?auto=format&fit=crop&w=500&q=80", BadgeText = "Fruity" },
                            new MenuItemDto { Id = 22, Name = "Chai Tea Latte", Price = 145.00m, Description = "Black tea infused with cinnamon, clove, and warm spices, combined with steamed milk.", CategorySlug = "teavana-tea", CategoryName = "Teavana Tea", ImageUrl = "https://images.unsplash.com/photo-1564890369478-c89ca6d9cde9?auto=format&fit=crop&w=500&q=80", BadgeText = "Spiced" },
                            new MenuItemDto { Id = 23, Name = "Earl Grey Black Tea", Price = 115.00m, Description = "Classic black tea blend with aromatic bergamot citrus essential oil.", CategorySlug = "teavana-tea", CategoryName = "Teavana Tea", ImageUrl = "https://images.unsplash.com/photo-1571934811356-5cc561d6821f?auto=format&fit=crop&w=500&q=80", BadgeText = "Aromatic" }
                        }
                    },

                    // 6. ALL-DAY BAKERY (5 Unique Items)
                    new CategoryGroupDto
                    {
                        Slug = "all-day-bakery",
                        Name = "All-day Bakery",
                        Group = "Food",
                        ImageUrl = "https://images.unsplash.com/photo-1555507036-ab1f4038808a?auto=format&fit=crop&w=500&q=80",
                        Items = new List<MenuItemDto>
                        {
                            new MenuItemDto { Id = 24, Name = "Artisan Butter Croissant", Price = 95.00m, Description = "Golden, flaky, buttery classic French pastry baked fresh daily on campus.", CategorySlug = "all-day-bakery", CategoryName = "All-day Bakery", ImageUrl = "https://images.unsplash.com/photo-1555507036-ab1f4038808a?auto=format&fit=crop&w=500&q=80", BadgeText = "Fresh Baked" },
                            new MenuItemDto { Id = 25, Name = "Double Chocolate Muffin", Price = 85.00m, Description = "Rich Belgian chocolate batter filled with melted dark chocolate chunks.", CategorySlug = "all-day-bakery", CategoryName = "All-day Bakery", ImageUrl = "https://images.unsplash.com/photo-1607958996333-41aef7caefaa?auto=format&fit=crop&w=500&q=80", BadgeText = "Sweet" },
                            new MenuItemDto { Id = 26, Name = "Blueberry Cream Cheese Danish", Price = 105.00m, Description = "Flaky puff pastry centered with sweet cream cheese and wild blueberry glaze.", CategorySlug = "all-day-bakery", CategoryName = "All-day Bakery", ImageUrl = "https://images.unsplash.com/photo-1509440159596-0249088772ff?auto=format&fit=crop&w=500&q=80", BadgeText = "Staff Pick" },
                            new MenuItemDto { Id = 27, Name = "Ham & Cheese Croissant Roll", Price = 120.00m, Description = "Savory flaky croissant wrapped around smoked ham slice and melted cheddar.", CategorySlug = "all-day-bakery", CategoryName = "All-day Bakery", ImageUrl = "https://images.unsplash.com/photo-1530610476181-d83430b64dcd?auto=format&fit=crop&w=500&q=80", BadgeText = "Savory" },
                            new MenuItemDto { Id = 28, Name = "Cinnamon Roll with Cream Glaze", Price = 95.00m, Description = "Warm rolled dough infused with Korintje cinnamon, topped with vanilla frosting.", CategorySlug = "all-day-bakery", CategoryName = "All-day Bakery", ImageUrl = "https://images.unsplash.com/photo-1509365465985-25d11c17e812?auto=format&fit=crop&w=500&q=80", BadgeText = "Warm" }
                        }
                    },

                    // 7. SANDWICHES & PASTAS 
                    new CategoryGroupDto
                    {
                        Slug = "sandwiches-pastas",
                        Name = "Sandwiches & Pastas",
                        Group = "Food",
                        ImageUrl = "https://images.unsplash.com/photo-1528735602780-2552fd46c7af?auto=format&fit=crop&w=500&q=80",
                        Items = new List<MenuItemDto>
                        {
                            new MenuItemDto { Id = 29, Name = "Grilled Chicken Pesto Panini", Price = 165.00m, Description = "Grilled chicken breast, basil pesto, and mozzarella pressed on ciabatta bread.", CategorySlug = "sandwiches-pastas", CategoryName = "Sandwiches & Pastas", ImageUrl = "https://images.unsplash.com/photo-1528735602780-2552fd46c7af?auto=format&fit=crop&w=500&q=80", BadgeText = "Hot Panini" },
                            new MenuItemDto { Id = 30, Name = "Smoked Bacon & Egg Sandwich", Price = 135.00m, Description = "Crispy bacon, fluffy egg souffle, and cheddar cheese on brioche bun.", CategorySlug = "sandwiches-pastas", CategoryName = "Sandwiches & Pastas", ImageUrl = "https://images.unsplash.com/photo-1550547660-d9450f859349?auto=format&fit=crop&w=500&q=80", BadgeText = "Breakfast" },
                            new MenuItemDto { Id = 31, Name = "Creamy Carbonara Pasta", Price = 175.00m, Description = "Penne pasta tossed in rich parmesan egg cream sauce with bacon bits.", CategorySlug = "sandwiches-pastas", CategoryName = "Sandwiches & Pastas", ImageUrl = "https://images.unsplash.com/photo-1612874742237-6526221588e3?auto=format&fit=crop&w=500&q=80", BadgeText = "Pasta" },
                            new MenuItemDto { Id = 32, Name = "Triple Cheese Melt Panini", Price = 145.00m, Description = "Melted mozzarella, sharp cheddar, and provolone on toasted sourdough.", CategorySlug = "sandwiches-pastas", CategoryName = "Sandwiches & Pastas", ImageUrl = "https://images.unsplash.com/photo-1539252554453-80ab65ce3586?auto=format&fit=crop&w=500&q=80", BadgeText = "Cheesy" },
                            new MenuItemDto { Id = 33, Name = "Tuna Mayo Salad Sandwich", Price = 130.00m, Description = "Flaked tuna, herbs, celery, and creamy mayo served on whole wheat bread.", CategorySlug = "sandwiches-pastas", CategoryName = "Sandwiches & Pastas", ImageUrl = "https://images.unsplash.com/photo-1509722747041-616f39b57569?auto=format&fit=crop&w=500&q=80", BadgeText = "Healthy" }
                        }
                    },

                    // 8. DESSERTS (5 Unique Items)
                    new CategoryGroupDto
                    {
                        Slug = "desserts",
                        Name = "Desserts",
                        Group = "Food",
                        ImageUrl = "https://images.unsplash.com/photo-1578985545062-69928b1d9587?auto=format&fit=crop&w=500&q=80",
                        Items = new List<MenuItemDto>
                        {
                            new MenuItemDto { Id = 34, Name = "Dark Chocolate Fudge Cake", Price = 140.00m, Description = "Rich, moist chocolate cake layered with dark chocolate ganache frosting.", CategorySlug = "desserts", CategoryName = "Desserts", ImageUrl = "https://images.unsplash.com/photo-1578985545062-69928b1d9587?auto=format&fit=crop&w=500&q=80", BadgeText = "Best Cake" },
                            new MenuItemDto { Id = 35, Name = "New York Baked Cheesecake", Price = 150.00m, Description = "Dense, creamy classic cheesecake on graham cracker crust crumble.", CategorySlug = "desserts", CategoryName = "Desserts", ImageUrl = "https://images.unsplash.com/photo-1533134242443-d4fd215305ad?auto=format&fit=crop&w=500&q=80", BadgeText = "Classic" },
                            new MenuItemDto { Id = 36, Name = "Red Velvet Cake Slice", Price = 145.00m, Description = "Vibrant red cocoa cake layers paired with smooth cream cheese frosting.", CategorySlug = "desserts", CategoryName = "Desserts", ImageUrl = "https://images.unsplash.com/photo-1586788680434-30d324b2d46f?auto=format&fit=crop&w=500&q=80", BadgeText = "Popular" },
                            new MenuItemDto { Id = 37, Name = "Classic Tiramisu Cup", Price = 135.00m, Description = "Espresso-soaked ladyfingers layered with mascarpone cheese and dusted cocoa powder.", CategorySlug = "desserts", CategoryName = "Desserts", ImageUrl = "https://images.unsplash.com/photo-1571877227200-a0d98ea607e9?auto=format&fit=crop&w=500&q=80", BadgeText = "Italian" },
                            new MenuItemDto { Id = 38, Name = "Fudge Walnut Brownie", Price = 90.00m, Description = "Chewy chocolate brownie square topped with roasted walnut chunks.", CategorySlug = "desserts", CategoryName = "Desserts", ImageUrl = "https://images.unsplash.com/photo-1606313564200-e75d5e30476c?auto=format&fit=crop&w=500&q=80", BadgeText = "Fudgy" }
                        }
                    }
                }
            };
        }
    }
}