# SafeTravels
SafeTravels is a android mobile app built with .NET MAUI that helps users explore and manage public transport options with ease. Designed with accessibility and usability in mind, it provides real-time bus stop details, trip filtering, and intuitive navigation — all while supporting screen readers like TalkBack.
---
## Features

-  **Search & Filter Trips**: Quickly find bus routes using a responsive search bar and filter options.
- **Bus Stop Details**: View stop names, IDs, and upcoming arrivals in a clean, card-based layout.
- **Save Favorite Stops**: Bookmark frequently used stops for faster access.
- **Accessible by Design**: Fully compatible with TalkBack (Android) using `SemanticProperties` for screen reader support.

---

## Accessibility Highlights

SafeTravels is designed to be inclusive:

- `SemanticProperties.Description` and `Hint` used throughout the UI
- Logical navigation order using `TabIndex`
- Dynamic updates (e.g., trip status) support `LiveRegion` for screen reader announcements
- All interactive elements are labeled and described for clarity

## Tech

- **Framework**: [.NET MAUI](https://learn.microsoft.com/en-us/dotnet/maui/overview)
- **Language**: C# with XAML UI
- **Architecture**: MVVM pattern with data binding
- **Platform Support**: Android
