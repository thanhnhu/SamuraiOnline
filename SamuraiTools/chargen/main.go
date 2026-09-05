package main

import (
	"flag"
	"fmt"
	"image"
	"log"
	"os"
	"path/filepath"
)

func main() {
	srcPath := flag.String("src", "", "path to ModulePlayer.cpp")
	sheetPath := flag.String("sheet", "", "path to the character sprite sheet PNG")
	outDir := flag.String("out", "", "output character directory")
	name := flag.String("name", "Jubei", "character display name")
	base := flag.String("base", "jubei", "base file name inside the character directory")
	flag.Parse()

	if *srcPath == "" || *sheetPath == "" || *outDir == "" {
		flag.Usage()
		os.Exit(2)
	}

	data, err := os.ReadFile(*srcPath)
	if err != nil {
		log.Fatalf("read source: %v", err)
	}
	src, err := ParseSource(string(data))
	if err != nil {
		log.Fatalf("parse source: %v", err)
	}

	sheet, err := LoadPNG(*sheetPath)
	if err != nil {
		log.Fatalf("read sheet: %v", err)
	}
	bounds := sheet.Bounds()

	actions := BuildActions(src)
	attacks := BuildAttacks(src)
	specials := BuildSpecials(src)

	var sprites []SffSprite
	skipped := 0
	for _, a := range actions {
		for i, f := range a.Frames {
			if !fitsInside(bounds, f.Src) {
				// A rect outside the sheet means the table refers to art that
				// was never shipped; dropping the frame beats emitting garbage.
				log.Printf("warn: %s frame %d rect %+v lies outside the sheet, skipped",
					a.Name, i, f.Src)
				skipped++
				continue
			}
			ax, ay := axis(f)
			sprites = append(sprites, SffSprite{
				Group:  uint16(a.No),
				Number: uint16(i),
				AxisX:  ax,
				AxisY:  ay,
				Img:    CropSheet(sheet, f.Src),
			})
		}
	}

	// Ikemen needs a small and a large portrait for the select screen.
	if len(sprites) > 0 {
		sprites = append(sprites,
			SffSprite{Group: 9000, Number: 0, AxisX: sprites[0].AxisX, AxisY: sprites[0].AxisY, Img: sprites[0].Img},
			SffSprite{Group: 9000, Number: 1, AxisX: sprites[0].AxisX, AxisY: sprites[0].AxisY, Img: sprites[0].Img},
		)
	}

	if err := os.MkdirAll(*outDir, 0o755); err != nil {
		log.Fatalf("create output directory: %v", err)
	}
	write := func(ext, content string) {
		p := filepath.Join(*outDir, *base+ext)
		if err := os.WriteFile(p, []byte(content), 0o644); err != nil {
			log.Fatalf("write %s: %v", p, err)
		}
	}

	if err := WriteSff(filepath.Join(*outDir, *base+".sff"), sprites); err != nil {
		log.Fatalf("write sff: %v", err)
	}
	write(".air", WriteAir(actions, *name))
	write(".cns", WriteCns(src, attacks, specials))
	write(".cmd", WriteCmd(attacks, specials))
	write(".def", WriteDef(*name, *base))

	fmt.Printf("animations : %d\n", len(actions))
	fmt.Printf("sprites    : %d (%d skipped)\n", len(sprites), skipped)
	fmt.Printf("attacks    : %d\n", len(attacks))
	fmt.Printf("specials   : %d\n", len(specials))
	fmt.Printf("output     : %s\n", *outDir)
}

func fitsInside(b image.Rectangle, r Rect) bool {
	return r.W > 0 && r.H > 0 &&
		r.X >= b.Min.X && r.Y >= b.Min.Y &&
		r.X+r.W <= b.Max.X && r.Y+r.H <= b.Max.Y
}
