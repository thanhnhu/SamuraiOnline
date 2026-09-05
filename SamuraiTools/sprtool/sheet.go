package main

import (
	"image"
	"image/color"
	"image/draw"
	"image/png"
	"os"
	"sort"
)

// A contact sheet turns "are these hundreds of sprites decoded correctly?" into
// a single glance. Misparsed sprites stand out immediately as noise or streaks
// among the recognisable poses.

func contactSheet(sprites []*Sprite, cols, cell int, pals []color.Palette) *image.RGBA {
	if len(sprites) == 0 {
		return image.NewRGBA(image.Rect(0, 0, 1, 1))
	}
	rows := (len(sprites) + cols - 1) / cols
	out := image.NewRGBA(image.Rect(0, 0, cols*cell, rows*cell))
	draw.Draw(out, out.Bounds(), &image.Uniform{color.RGBA{24, 24, 32, 255}}, image.Point{}, draw.Src)

	for i, s := range sprites {
		if s == nil {
			continue
		}
		pal := pals[i]
		cx := (i % cols) * cell
		cy := (i / cols) * cell

		// Fit inside the cell without upscaling, so small sprites stay crisp.
		scale := 1
		for scale > 1 && (s.Width*scale > cell || s.Height*scale > cell) {
			scale--
		}
		step := 1
		for s.Width/step > cell || s.Height/step > cell {
			step++
		}

		for y := 0; y < s.Height/step && y < cell; y++ {
			for x := 0; x < s.Width/step && x < cell; x++ {
				p := (y*step)*s.Width + (x * step)
				if !s.Opaque[p] {
					continue
				}
				idx := int(s.Pix[p])
				if idx >= len(pal) {
					continue
				}
				out.Set(cx+x, cy+y, pal[idx])
			}
		}
	}
	return out
}

func writeSheet(path string, sprites []*Sprite, cols, cell int, pals []color.Palette) error {
	f, err := os.Create(path)
	if err != nil {
		return err
	}
	defer f.Close()
	return png.Encode(f, contactSheet(sprites, cols, cell, pals))
}

// groupByBank keeps sprites of one bank together, since a bank is the unit that
// shares a palette and usually a purpose.
func groupByBank(descs []Desc) map[uint16][]Desc {
	out := map[uint16][]Desc{}
	for _, d := range descs {
		out[d.Bank] = append(out[d.Bank], d)
	}
	for _, v := range out {
		sort.Slice(v, func(i, j int) bool { return v[i].DataOffset < v[j].DataOffset })
	}
	return out
}
