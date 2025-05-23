# ChestContents

![Effect Example](https://raw.githubusercontent.com/Acen/ChestContents/master/Assets/effect_example.jpg)

![Meta Pane Example](https://raw.githubusercontent.com/Acen/ChestContents/master/Assets/meta_detail.jpg)

## Introduction

**ChestContents** is a Valheim mod/plugin that helps you keep track of all your chests and their contents. It provides an at-a-glance overview of how many chests are indexed and what items are stored where, making inventory management much easier.

## Features

- Displays the number of indexed chests as a status effect (see the first image above).
- Provides a searchable meta pane to quickly find where items are stored (see the second image above).
- Command-line interface for searching chests by item name.

## Commands

### Search Chests Command

You can search for items across all indexed chests using the following command:

```
/searchchests <item name>
```

#### Aliases
- `/cs`
- `/sc`

#### Example Usage

To find the chest containing the most mushrooms, type:

```
/searchchests mushroom
```

This will indicate the chest with the highest quantity of mushrooms in your indexed chests, showing its location and the quantity found.

## Best Practices

- Use the search command with partial item names for broader results (e.g., `/chestsearch mush` will match "mushroom").

## Installation

1. Download the latest release of `ChestContents.dll` and the matching named folder (`ChestContents`).
2. Place them in your `BepInEx/plugins` folder.
3. Launch Valheim and enjoy easier item finding!

## Contributing

Pull requests and suggestions are welcome! Please open an issue to discuss any major changes before submitting a PR.

## License

MIT
