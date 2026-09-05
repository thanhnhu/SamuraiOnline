package main

import (
	"flag"
	"fmt"
	"image/color"
	"log"
	"os"
)

func main() {
	path := flag.String("f", "", "file to inspect")
	dump := flag.Int("dump", 256, "bytes to hex dump")
	at := flag.Int("at", 0, "offset to dump from")
	recFrom := flag.Int("rec", 0, "first record index to describe")
	onlyLive := flag.Bool("live", false, "describe only populated records")
	decodeAt := flag.Int("decode", -1, "offset to decode a single sprite from")
	traceAt := flag.Int("trace", -1, "offset to trace row by row")
	extractDir := flag.String("x", "", "directory to extract all sprites into")
	sheetPath := flag.String("sheet", "", "write a contact sheet here")
	limit := flag.Int("limit", 0, "stop after this many sprites (0 = all)")
	hist := flag.Bool("hist", false, "report palette index usage and bank alignment")
	palScan := flag.Bool("palscan", false, "search the file for RGB555 palette tables")
	palSwatch := flag.String("palswatch", "", "write candidate palettes as swatches here")
	palAt := flag.Int("palat", -1, "dump palettes starting at this offset")
	palFile := flag.String("pal", "", "file holding the 4096-entry palette table")
	palBase := flag.Int("palbase", 0, "offset of the palette table inside -pal")
	palSweep := flag.String("palsweep", "", "render one sprite under every palette, written here")
	sweepSprite := flag.Int("sprite", 0, "sprite index to use for -palsweep")
	palSets := flag.String("palsets", "", "render one sprite under each palette snapshot, written here")
	w := flag.Int("w", 64, "sprite width when decoding or tracing")
	h := flag.Int("h", 112, "sprite height when decoding or tracing")
	out := flag.String("o", "", "PNG to write a decoded sprite to")
	flag.Parse()

	if *path == "" {
		flag.Usage()
		os.Exit(2)
	}
	b, err := os.ReadFile(*path)
	if err != nil {
		log.Fatal(err)
	}

	var ram *paletteRAM
	var palBytes []byte
	if *palFile != "" {
		pb, err := os.ReadFile(*palFile)
		if err != nil {
			log.Fatal(err)
		}
		palBytes = pb
		ram = loadPaletteRAM(pb, *palBase)
	}

	switch {
	case *palSets != "":
		if palBytes == nil {
			log.Fatal("-palsets needs -pal")
		}
		n := *limit
		if n == 0 {
			n = 6
		}
		runPalSets(b, palBytes, *palSets, *sweepSprite, *palBase, n)
	case *palSweep != "":
		if ram == nil {
			log.Fatal("-palsweep needs -pal")
		}
		n := *limit
		if n == 0 {
			n = 256
		}
		runPalSweep(b, *palSweep, *sweepSprite, n, ram)
	case *palScan || *palSwatch != "" || *palAt >= 0:
		runPalette(b, *path, *palScan, *palSwatch, *palAt, *limit)
	case *hist:
		fmt.Print(collectStats(b, *limit))
	case *decodeAt >= 0:
		runDecode(b, *decodeAt, *w, *h, *out)
	case *traceAt >= 0:
		fmt.Print(reportTrace(b, *traceAt, *w, *h, 16))
	case *extractDir != "":
		runExtract(b, *extractDir, *limit, ram)
	case *sheetPath != "":
		runSheet(b, *sheetPath, *limit, ram)
	default:
		runInspect(b, *path, *at, *dump, *recFrom, *onlyLive)
	}
}

func runDecode(b []byte, off, w, h int, out string) {
	s, used, err := decodeSprite(b, off, w, h, false)
	if err != nil {
		log.Fatalf("decode at 0x%X: %v (consumed %d bytes)", off, err, used)
	}
	fmt.Printf("decoded %dx%d from 0x%X, consumed %d bytes, %.1f%% opaque\n",
		s.Width, s.Height, off, used, s.coverage()*100)
	if out != "" {
		if err := writePNG(out, s, debugPalette()); err != nil {
			log.Fatal(err)
		}
		fmt.Println("wrote", out)
	}
}

func runExtract(b []byte, dir string, limit int, ram *paletteRAM) {
	hdr, descs := mustDescribe(b)
	if err := os.MkdirAll(dir, 0o755); err != nil {
		log.Fatal(err)
	}
	fmt.Print(summariseDescriptors(descs))

	ok, failed := 0, 0
	for i, d := range descs {
		if limit > 0 && i >= limit {
			break
		}
		pal := debugPalette()
		if ram != nil {
			pal = ram.forBank(d.Bank)
		}
		s, err := Extract(b, hdr, d)
		if err != nil {
			failed++
			continue
		}
		name := fmt.Sprintf("%s/%05d_%03dx%03d_p%04d.png", dir, d.Index, d.Width, d.Height, d.Bank)
		if err := writePNG(name, s, pal); err != nil {
			log.Fatal(err)
		}
		ok++
	}
	fmt.Printf("  extracted %d, failed %d\n", ok, failed)
}

func runSheet(b []byte, path string, limit int, ram *paletteRAM) {
	hdr, descs := mustDescribe(b)
	fmt.Print(summariseDescriptors(descs))

	var sprites []*Sprite
	var pals []color.Palette
	failed := 0
	for i, d := range descs {
		if limit > 0 && i >= limit {
			break
		}
		pal := debugPalette()
		if ram != nil {
			pal = ram.forBank(d.Bank)
		}
		s, err := Extract(b, hdr, d)
		if err != nil {
			failed++
			sprites = append(sprites, nil)
			pals = append(pals, pal)
			continue
		}
		sprites = append(sprites, s)
		pals = append(pals, pal)
	}
	if err := writeSheet(path, sprites, 24, 72, pals); err != nil {
		log.Fatal(err)
	}
	fmt.Printf("  wrote %s with %d cells, %d failed\n", path, len(sprites), failed)
}

func runPalette(b []byte, path string, scan bool, swatch string, at, limit int) {
	if scan {
		fmt.Print(reportPaletteTables(b, path, 4))
		fmt.Print(reportColourRuns(b, path, 12))
	}
	if swatch == "" {
		return
	}
	var cands []palCandidate
	if at >= 0 {
		n := limit
		if n == 0 {
			n = 64
		}
		for i := 0; i < n; i++ {
			off := at + i*palEntries*2
			if off+palEntries*2 > len(b) {
				break
			}
			c := palCandidate{Off: off}
			for j := 0; j < palEntries; j++ {
				w := uint16(b[off+j*2]) | uint16(b[off+j*2+1])<<8
				c.Words[j] = w
				c.Colors[j] = rgb555(w)
			}
			cands = append(cands, c)
		}
	} else {
		cands = scanPalettes(b, palEntries*2)
	}
	n := limit
	if n == 0 {
		n = 64
	}
	if err := writePaletteSwatches(swatch, cands, n); err != nil {
		log.Fatal(err)
	}
	fmt.Printf("  wrote %s with %d palettes\n", swatch, len(cands))
}

// runPalSweep renders one sprite under every palette in the table. Any base
// that is congruent mod 32 produces internally consistent colours, so a single
// good-looking sheet proves nothing; only comparing all of them shows which
// palette the sprite was actually authored against.
func runPalSweep(b []byte, path string, sprite, count int, ram *paletteRAM) {
	hdr, descs := mustDescribe(b)
	if sprite >= len(descs) {
		log.Fatalf("sprite %d out of range (%d present)", sprite, len(descs))
	}
	s, err := Extract(b, hdr, descs[sprite])
	if err != nil {
		log.Fatal(err)
	}
	fmt.Printf("  sprite %d: %dx%d, bank %d\n", sprite, s.Width, s.Height, descs[sprite].Bank)

	var sprites []*Sprite
	var pals []color.Palette
	for i := 0; i < count; i++ {
		sprites = append(sprites, s)
		pals = append(pals, ram.forBank(uint16(i*palEntries)))
	}
	if err := writeSheet(path, sprites, 16, 80, pals); err != nil {
		log.Fatal(err)
	}
	fmt.Printf("  wrote %s: palettes 0..%d\n", path, count-1)
}

// runPalSets renders one sprite under the same palette slot taken from each
// consecutive palette RAM snapshot. A sprite's bank is fixed, so the only
// question is which snapshot was live when that sprite was on screen.
func runPalSets(b, pb []byte, path string, sprite, base, count int) {
	hdr, descs := mustDescribe(b)
	if sprite >= len(descs) {
		log.Fatalf("sprite %d out of range (%d present)", sprite, len(descs))
	}
	d := descs[sprite]
	s, err := Extract(b, hdr, d)
	if err != nil {
		log.Fatal(err)
	}
	fmt.Printf("  sprite %d: %dx%d, bank %d (palette %d)\n",
		sprite, s.Width, s.Height, d.Bank, d.Bank/palEntries)

	const snapshot = 4096 * 2
	var sprites []*Sprite
	var pals []color.Palette
	for i := 0; i < count; i++ {
		sprites = append(sprites, s)
		pals = append(pals, loadPaletteRAM(pb, base+i*snapshot).forBank(d.Bank))
	}
	if err := writeSheet(path, sprites, count, 160, pals); err != nil {
		log.Fatal(err)
	}
	fmt.Printf("  wrote %s: snapshots 0..%d\n", path, count-1)
}

func runInspect(b []byte, path string, at, dump, recFrom int, onlyLive bool) {
	fmt.Printf("file    : %s\n", path)
	fmt.Printf("size    : %d bytes (0x%X)\n", len(b), len(b))

	hdr, err := ParseHeader(b)
	if err != nil {
		fmt.Println("header  :", err)
	} else {
		fmt.Printf("magic   : 0x%04X\n", hdr.Magic)
		fmt.Printf("sprites : %d\n", hdr.Count)
		fmt.Printf("data    : %d bytes at 0x%X\n", hdr.DataSize, hdr.DataOffset())
		if err := hdr.Check(len(b)); err != nil {
			fmt.Println("check   :", err)
		} else {
			fmt.Println("check   : layout consistent")
		}
		recs := ReadRecords(b, hdr)
		live := NonEmpty(recs)
		fmt.Printf("records : %d read, %d populated\n", len(recs), len(live))
		fmt.Println()
		if onlyLive {
			fmt.Print(describeRecords(live, recFrom, 16))
		} else {
			fmt.Print(describeRecords(recs, recFrom, 12))
		}
	}

	if dump > 0 {
		fmt.Println()
		fmt.Println(hexDump(b, at, dump))
	}
}

func mustDescribe(b []byte) (Header, []Desc) {
	hdr, err := ParseHeader(b)
	if err != nil {
		log.Fatal(err)
	}
	return hdr, Descriptors(b, hdr)
}
