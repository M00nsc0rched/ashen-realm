# Cuts the menu art out of the design sketches (the Vazlat_*.jpg files next to the project folder, and the folder of class
# cards, the tavern and the city pictures beside them) into Assets/Art/Menu.
# The results are committed, so this only needs to run again when a sketch changes. System.Drawing, no other tools.
# Usage (from the project folder):  powershell -ExecutionPolicy Bypass -File tools/prepare-menu-art.ps1
param(
    [string]$Sketches = (Join-Path $PSScriptRoot "..\.."),
    [string]$Out = (Join-Path $PSScriptRoot "..\Assets\Art\Menu")
)

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static class MenuArt
{
    public static Bitmap Crop(Bitmap source, int x, int y, int w, int h)
    {
        var result = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(result))
            g.DrawImage(source, new Rectangle(0, 0, w, h), new Rectangle(x, y, w, h), GraphicsUnit.Pixel);
        return result;
    }

    // Gold on black: brightness becomes alpha and the colour is un-premultiplied, so the art composites onto any dark panel.
    static Color Keyed(Color c, int black, int white)
    {
        int max = Math.Max(c.R, Math.Max(c.G, c.B));
        float a = Math.Min(1f, Math.Max(0f, (max - black) / (float)(white - black)));
        if (a <= 0f) return Color.FromArgb(0, 0, 0, 0);
        return Color.FromArgb((int)(a * 255f), Math.Min(255, (int)(c.R / a)), Math.Min(255, (int)(c.G / a)), Math.Min(255, (int)(c.B / a)));
    }

    public static void Key(Bitmap image, int black, int white)
    {
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
            image.SetPixel(x, y, Keyed(image.GetPixel(x, y), black, white));
    }

    // Keeps a button's face opaque and keys only what lies outside its chamfered frame (the margin and the cut corner).
    public static void KeyOutsideFrame(Bitmap corner, int margin, int chamfer, int black, int white)
    {
        for (int y = 0; y < corner.Height; y++)
        for (int x = 0; x < corner.Width; x++)
        {
            bool inside = x >= margin && y >= margin && x + y >= margin * 2 + chamfer;
            if (!inside) corner.SetPixel(x, y, Keyed(corner.GetPixel(x, y), black, white));
        }
    }

    // Mirrors one corner into all four, with a stretch band between them: the sketch's frames are symmetric and this way
    // the slices need no text-free edge from the sketch.
    public static Bitmap NineSlice(Bitmap corner, int band)
    {
        int c = corner.Width, size = c * 2 + band;
        var result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int sx = x < c ? x : x < c + band ? c - 1 : size - 1 - x;
            int sy = y < c ? y : y < c + band ? c - 1 : size - 1 - y;
            result.SetPixel(x, y, corner.GetPixel(sx, sy));
        }
        return result;
    }

    // Fills rectangles from the pixels around them, blurred (pull-push over a pyramid). Enough for what lies under cards and text.
    public static void FillHoles(Bitmap image, Rectangle[] holes)
    {
        int w = image.Width, h = image.Height;
        var colours = new List<float[,,]>();
        var weights = new List<float[,]>();

        var colour = new float[w, h, 3];
        var weight = new float[w, h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            bool hole = false;
            foreach (Rectangle r in holes) hole |= r.Contains(x, y);
            if (hole) continue;
            Color c = image.GetPixel(x, y);
            colour[x, y, 0] = c.R; colour[x, y, 1] = c.G; colour[x, y, 2] = c.B;
            weight[x, y] = 1f;
        }

        while (true)
        {
            colours.Add(colour);
            weights.Add(weight);
            if (w == 1 && h == 1) break;
            int nw = Math.Max(1, w / 2), nh = Math.Max(1, h / 2);
            var coarse = new float[nw, nh, 3];
            var coarseWeight = new float[nw, nh];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int cx = Math.Min(nw - 1, x / 2), cy = Math.Min(nh - 1, y / 2);
                float k = weight[x, y];
                coarseWeight[cx, cy] += k;
                for (int i = 0; i < 3; i++) coarse[cx, cy, i] += colour[x, y, i] * k;
            }
            for (int y = 0; y < nh; y++)
            for (int x = 0; x < nw; x++)
                if (coarseWeight[x, y] > 0f)
                {
                    for (int i = 0; i < 3; i++) coarse[x, y, i] /= coarseWeight[x, y];
                    coarseWeight[x, y] = Math.Min(1f, coarseWeight[x, y]);
                }
            colour = coarse; weight = coarseWeight; w = nw; h = nh;
        }

        for (int level = colours.Count - 2; level >= 0; level--)
        {
            float[,,] fine = colours[level], coarse = colours[level + 1];
            float[,] fineWeight = weights[level];
            int fw = fine.GetLength(0), fh = fine.GetLength(1), cw = coarse.GetLength(0), ch = coarse.GetLength(1);
            for (int y = 0; y < fh; y++)
            for (int x = 0; x < fw; x++)
            {
                float k = fineWeight[x, y];
                if (k >= 1f) continue;
                float gx = Math.Max(0f, Math.Min(cw - 1f, (x + 0.5f) * cw / fw - 0.5f));
                float gy = Math.Max(0f, Math.Min(ch - 1f, (y + 0.5f) * ch / fh - 0.5f));
                int x0 = (int)gx, y0 = (int)gy, x1 = Math.Min(cw - 1, x0 + 1), y1 = Math.Min(ch - 1, y0 + 1);
                float tx = gx - x0, ty = gy - y0;
                for (int i = 0; i < 3; i++)
                {
                    float top = coarse[x0, y0, i] * (1f - tx) + coarse[x1, y0, i] * tx;
                    float bottom = coarse[x0, y1, i] * (1f - tx) + coarse[x1, y1, i] * tx;
                    fine[x, y, i] = fine[x, y, i] * k + (top * (1f - ty) + bottom * ty) * (1f - k);
                }
            }
        }

        foreach (Rectangle r in holes)
            for (int y = Math.Max(0, r.Top); y < Math.Min(image.Height, r.Bottom); y++)
            for (int x = Math.Max(0, r.Left); x < Math.Min(image.Width, r.Right); x++)
                image.SetPixel(x, y, Color.FromArgb((int)colours[0][x, y, 0], (int)colours[0][x, y, 1], (int)colours[0][x, y, 2]));
    }

    // Fills and darkens the rectangles, and lets the painting dissolve into them, so no seam shows around the cards.
    public static void FadeHoles(Bitmap image, Rectangle[] holes, int[] feathers, float darkness)
    {
        var grown = new Rectangle[holes.Length];
        for (int i = 0; i < holes.Length; i++) grown[i] = Rectangle.Inflate(holes[i], feathers[i], feathers[i]);
        using (var fill = new Bitmap(image))
        {
            FillHoles(fill, grown);
            for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
            {
                float t = 0f;
                for (int i = 0; i < holes.Length; i++)
                {
                    Rectangle r = holes[i];
                    int dx = Math.Max(Math.Max(r.Left - x, 0), x - (r.Right - 1));
                    int dy = Math.Max(Math.Max(r.Top - y, 0), y - (r.Bottom - 1));
                    t = Math.Max(t, 1f - (float)Math.Sqrt(dx * dx + dy * dy) / feathers[i]);
                }
                if (t <= 0f) continue;
                t = t * t * (3f - 2f * t);
                Color a = image.GetPixel(x, y), b = fill.GetPixel(x, y);
                image.SetPixel(x, y, Color.FromArgb(
                    (int)(a.R + (b.R * darkness - a.R) * t), (int)(a.G + (b.G * darkness - a.G) * t), (int)(a.B + (b.B * darkness - a.B) * t)));
            }
        }
    }

    // The frame without its ornaments: the rows and columns that are mostly solid.
    public static Rectangle FrameOf(Bitmap card)
    {
        int left = -1, right = -1, top = -1, bottom = -1;
        for (int x = 0; x < card.Width; x++)
        {
            int solid = 0;
            for (int y = 0; y < card.Height; y += 2) if (card.GetPixel(x, y).A > 200) solid++;
            if (solid > card.Height * 0.34f) { if (left < 0) left = x; right = x; }
        }
        for (int y = 0; y < card.Height; y++)
        {
            int solid = 0;
            for (int x = 0; x < card.Width; x += 2) if (card.GetPixel(x, y).A > 200) solid++;
            if (solid > card.Width * 0.35f) { if (top < 0) top = y; bottom = y; }
        }
        return Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    // Puts every card's frame on the same rectangle, so the frames line up when the cards turn. The source frames differ
    // by a few percent in shape, too little to see in the painting. Stray specks beside the frame are left out.
    public static Bitmap FitCard(Bitmap card, Rectangle frame, int width, int height, Rectangle target, int clipTop)
    {
        float sx = target.Width / (float)frame.Width, sy = target.Height / (float)frame.Height;
        var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(result))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.SetClip(new Rectangle(target.X - 6, clipTop, target.Width + 12, height - clipTop));
            g.DrawImage(card, new RectangleF(target.X - frame.X * sx, target.Y - frame.Y * sy, card.Width * sx, card.Height * sy),
                new RectangleF(0, 0, card.Width, card.Height), GraphicsUnit.Pixel);
        }
        return result;
    }

    // A square piece of a larger picture, at a new size.
    public static Bitmap Scale(Bitmap source, int x, int y, int side, int size)
    {
        var result = new Bitmap(size, size, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(result))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(source, new Rectangle(0, 0, size, size), new Rectangle(x, y, side, side), GraphicsUnit.Pixel);
        }
        return result;
    }

    // Everything solid, for art whose shape is not a card frame.
    public static Rectangle SolidBounds(Bitmap card)
    {
        int left = card.Width, right = -1, top = card.Height, bottom = -1;
        for (int y = 0; y < card.Height; y++)
        for (int x = 0; x < card.Width; x++)
        {
            if (card.GetPixel(x, y).A <= 200) continue;
            left = Math.Min(left, x); right = Math.Max(right, x);
            top = Math.Min(top, y); bottom = Math.Max(bottom, y);
        }
        return Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    // Cuts a thing out of its empty margin and scales its long side to size. DXT needs multiples of four, so the canvas grows to fit.
    public static Bitmap FitThing(Bitmap source, int size)
    {
        int left = source.Width, right = -1, top = source.Height, bottom = -1;
        for (int y = 0; y < source.Height; y++)
        for (int x = 0; x < source.Width; x++)
        {
            if (source.GetPixel(x, y).A <= 40) continue;
            left = Math.Min(left, x); right = Math.Max(right, x);
            top = Math.Min(top, y); bottom = Math.Max(bottom, y);
        }
        int w = right - left + 1, h = bottom - top + 1;
        float scale = size / (float)Math.Max(w, h);
        int drawnW = Math.Max(1, (int)(w * scale)), drawnH = Math.Max(1, (int)(h * scale));
        int canvasW = (drawnW + 3) / 4 * 4, canvasH = (drawnH + 3) / 4 * 4;
        var result = new Bitmap(canvasW, canvasH, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(result))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(source, new Rectangle((canvasW - drawnW) / 2, (canvasH - drawnH) / 2, drawnW, drawnH), new Rectangle(left, top, w, h), GraphicsUnit.Pixel);
        }
        return result;
    }

    // White with soft edges, tinted in the game: the light around the card in the middle.
    public static Bitmap SoftRect(int width, int height, int margin)
    {
        var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float edge = Math.Min(Math.Min(x + 0.5f, width - x - 0.5f), Math.Min(y + 0.5f, height - y - 0.5f));
            float t = Math.Min(1f, edge / margin);
            result.SetPixel(x, y, Color.FromArgb((int)(t * t * (3f - 2f * t) * 255f), 255, 255, 255));
        }
        return result;
    }
}
'@

function Find-Sketch([string]$pattern) {
    $file = Get-ChildItem -LiteralPath $Sketches -File | Where-Object { $_.Name -like $pattern } | Select-Object -First 1
    if ($null -eq $file) { throw "Sketch not found: $pattern in $Sketches" }
    return [System.Drawing.Bitmap]::FromFile($file.FullName)
}

function Save-Png($bitmap, [string]$name) {
    $bitmap.Save((Join-Path $Out $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
}

New-Item -ItemType Directory -Force -Path $Out | Out-Null
$Out = (Resolve-Path $Out).Path

# The sketches are named with accented letters; match around them so this file stays plain ASCII.
$main = Find-Sketch "V*zlat_F*perny*.jpg"
if ($main.Width -ne 1822) { $main.Dispose(); $main = Find-Sketch "V*zlat_F*perny?.jpg" }
$settings = Find-Sketch "V*zlat_Be*.jpg"
$clean = Find-Sketch "V*tiszta*.jpg"

function Save-Jpeg($bitmap, [string]$path) {
    $encoder = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq "image/jpeg" }
    $quality = New-Object System.Drawing.Imaging.EncoderParameters 1
    $quality.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), 92L
    $bitmap.Save($path, $encoder, $quality)
}

# Backgrounds: DXT needs multiples of four, so two columns go.
function Save-Background($bitmap, [string]$name) {
    $cropped = [MenuArt]::Crop($bitmap, 1, 0, 1844, 852)
    Save-Jpeg $cropped (Join-Path $Out $name)
    $cropped.Dispose()
}
Save-Background $clean "MenuBackground.jpg"

# Title and divider, keyed off the near-black panel.
$title = [MenuArt]::Crop($main, 100, 82, 544, 76)
[MenuArt]::Key($title, 26, 120)
Save-Png $title "MenuTitle.png"

$divider = [MenuArt]::Crop($main, 96, 166, 544, 24)
[MenuArt]::Key($divider, 26, 110)
Save-Png $divider "MenuDivider.png"

# Panel frame: thin gold lines, transparent inside.
$corner = [MenuArt]::Crop($main, 47, 51, 52, 52)
[MenuArt]::Key($corner, 30, 120)
Save-Png ([MenuArt]::NineSlice($corner, 4)) "MenuFrame.png"
$corner.Dispose()

# Buttons: same size and margin, so the highlight can swap in over the plain frame.
$corner = [MenuArt]::Crop($main, 72, 527, 42, 42)
[MenuArt]::KeyOutsideFrame($corner, 8, 7, 30, 120)
Save-Png ([MenuArt]::NineSlice($corner, 4)) "MenuButton.png"
$corner.Dispose()

# Right corner of the landscape option, flipped: its left corner holds the diamond marker.
$corner = [MenuArt]::Crop($settings, 869, 382, 42, 42)
$corner.RotateFlip([System.Drawing.RotateFlipType]::RotateNoneFlipX)
[MenuArt]::KeyOutsideFrame($corner, 8, 7, 34, 150)
Save-Png ([MenuArt]::NineSlice($corner, 4)) "MenuButtonGlow.png"
$corner.Dispose()

$main.Dispose()
$settings.Dispose()
$clean.Dispose()

# The class cards. The folder holds the full-size originals, too heavy to ship: a card on screen is about 400 units wide.
$cards = Get-ChildItem -LiteralPath $Sketches -Directory | Where-Object { $_.Name -like "V*zlat_K*rty*" } | Select-Object -First 1
if ($null -eq $cards) { throw "Card folder not found in $Sketches" }
$cardOut = Join-Path $Out "Cards"
New-Item -ItemType Directory -Force -Path $cardOut | Out-Null
$frame = New-Object System.Drawing.Rectangle(20, 72, 600, 828)
foreach ($pair in @(@("card_knight.png", "CardKnight"), @("card_gravethief.png", "CardGravethief"), @("card_witch.png", "CardWitch"),
                    @("card_warden.png", "CardWarden"), @("card_necromancer.png", "CardNecromancer"), @("card_alchemist.png", "CardAlchemist"))) {
    $source = [System.Drawing.Bitmap]::FromFile((Join-Path $cards.FullName $pair[0]))
    $fitted = [MenuArt]::FitCard($source, [MenuArt]::FrameOf($source), 640, 936, $frame, 0)
    $fitted.Save((Join-Path $cardOut ($pair[1] + ".png")), [System.Drawing.Imaging.ImageFormat]::Png)
    $fitted.Dispose()
    $source.Dispose()
}

# The sealed card is an arch with no ornaments: as wide as a frame, and tall enough to reach where they do.
$source = [System.Drawing.Bitmap]::FromFile((Join-Path $cards.FullName "card_locked.png"))
$arch = New-Object System.Drawing.Rectangle(20, 8, 600, 920)
$fitted = [MenuArt]::FitCard($source, [MenuArt]::SolidBounds($source), 640, 936, $arch, 8)
$fitted.Save((Join-Path $cardOut "CardLocked.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$fitted.Dispose()
$source.Dispose()

Save-Png ([MenuArt]::SoftRect(64, 92, 26)) "CardGlow.png"

# The class choice background: the concept's own edges, with everything drawn over its middle filled in.
$concept = [System.Drawing.Bitmap]::FromFile((Join-Path $cards.FullName "concept_knight.png"))
[System.Drawing.Rectangle[]]$holes = @(
    (New-Object System.Drawing.Rectangle(420, 0, 985, 112)),   # title and subtitle
    (New-Object System.Drawing.Rectangle(340, 112, 585, 630)), # the cards
    (New-Object System.Drawing.Rectangle(380, 742, 350, 62)),  # arrows, dots and hint
    (New-Object System.Drawing.Rectangle(925, 146, 632, 636))  # the details panel
)
[MenuArt]::FadeHoles($concept, $holes, [int[]](70, 90, 60, 26), 0.5)
Save-Background $concept "ChoiceBackground.jpg"
$concept.Dispose()

# The faces of Hollowgate: each strip shows one person three times, in the order wary, warm, hostile.
$tavern = Get-ChildItem -LiteralPath $Sketches -Directory | Where-Object { $_.Name -like "V*zlat_Kocsma" } | Select-Object -First 1
if ($null -eq $tavern) { throw "Tavern folder not found in $Sketches" }
$faceOut = Join-Path $Out "Portraits"
New-Item -ItemType Directory -Force -Path $faceOut | Out-Null
foreach ($person in @("wanderer", "landlord", "quartermaster", "gravedigger", "crier", "widow")) {
    $strip = [System.Drawing.Bitmap]::FromFile((Join-Path $tavern.FullName "faces_$person.png"))
    $cell = [int]($strip.Width / 3)
    for ($mood = 0; $mood -lt 3; $mood++) {
        # Inset, so the thin black gutter between the three pictures stays out.
        $face = [MenuArt]::Scale($strip, $mood * $cell + 10, 10, $cell - 20, 320)
        Save-Jpeg $face (Join-Path $faceOut ("{0}_{1}.jpg" -f $person, $mood))
        $face.Dispose()
    }
    $strip.Dispose()
}

# The goods of the Cold Forge and the Grey Chapel, cut to their shape for the shop tiles.
$city = Get-ChildItem -LiteralPath $Sketches -Directory | Where-Object { $_.Name -like "V*zlat_V*ros" } | Select-Object -First 1
if ($null -eq $city) { throw "City folder not found in $Sketches" }
$itemOut = Join-Path $Out "Items"
New-Item -ItemType Directory -Force -Path $itemOut | Out-Null
foreach ($item in @("rusted_blade", "iron_sword", "grave_dagger", "pilgrim_staff", "bloodstone", "witch_eye", "grave_idol")) {
    $source = [System.Drawing.Bitmap]::FromFile((Join-Path $city.FullName "item_$item.png"))
    $fitted = [MenuArt]::FitThing($source, 384)
    $fitted.Save((Join-Path $itemOut "$item.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $fitted.Dispose()
    $source.Dispose()
}

# The two who have no portrait strip are cut out of their rooms' paintings: one picture, whatever the soul's honor.
foreach ($cut in @(@("forge.png", 375, 250, 220, "smith"), @("chapel.png", 640, 250, 200, "choir"))) {
    $room = [System.Drawing.Bitmap]::FromFile((Join-Path $city.FullName $cut[0]))
    $face = [MenuArt]::Scale($room, $cut[1], $cut[2], $cut[3], 320)
    Save-Jpeg $face (Join-Path $faceOut ($cut[4] + "_0.jpg"))
    $face.Dispose()
    $room.Dispose()
}

Get-ChildItem -LiteralPath $Out -File -Recurse | Where-Object { $_.Extension -ne ".meta" } | ForEach-Object { "{0,-22} {1,8} bytes" -f $_.Name, $_.Length }
