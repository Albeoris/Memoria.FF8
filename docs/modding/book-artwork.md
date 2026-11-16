# Inventory book artwork

Add `items/book-artwork.json` to a native Memoria mod to make an inventory item open a PNG in the book viewer. Place the PNG in the same mod. For a downloadable package, both paths are relative to `payload/`.

```json
{
  "schemaVersion": 1,
  "items": [
    {
      "itemId": 222,
      "image": "art/covers/example.png"
    }
  ]
}
```

An item ID is a whole number from 0 to 255. Each ID may occur once per manifest. Use forward slashes and match the image filename's case. Images cannot reference files from other packages. PNGs may have transparency. The size limit is 64 MiB, with dimensions up to 8192 on either side and up to 32 million pixels.

The viewer uses the item's existing name, opens from the Item menu, and preserves inventory quantity. If more than one active package declares an item, the first package in the native mod order supplies the image. Disabling it reveals the next active declaration. An invalid binding prevents the candidate resource update from replacing the active one.

For official distribution, place the manifest and PNG in one `mods/<id>/payload/` directory in [Memoria.Mods](https://github.com/Albeoris/Memoria.Mods). Activation preferences belong to the player's installation.
