package main

import (
	"image"
	"image/color"
	"image/png"
	"os"
)

// Until the real palette is located, render indices through a spread of hues so
// the silhouette and internal shading are both readable. Getting the shape
// right is what validates the decoder; colour accuracy comes later.
func debugPalette() color.Palette {
	pal := make(color.Palette, 256)
	pal[0] = color.RGBA{0, 0, 0, 0}
	for i := 1; i < 256; i++ {
		v := uint8(40 + (i*7)%216)
		pal[i] = color.RGBA{
			R: v,
			G: uint8(40 + (i * 13 % 216)),
			B: uint8(40 + (i * 29 % 216)),
			A: 255,
		}
	}
	return pal
}

func writePNG(path string, s *Sprite, pal color.Palette) error {
	img := image.NewNRGBA(image.Rect(0, 0, s.Width, s.Height))
	for i, op := range s.Opaque {
		if !op {
			continue
		}
		img.Set(i%s.Width, i/s.Width, pal[s.Pix[i]])
	}

	f, err := os.Create(path)
	if err != nil {
		return err
	}
	defer f.Close()
	return png.Encode(f, img)
}
