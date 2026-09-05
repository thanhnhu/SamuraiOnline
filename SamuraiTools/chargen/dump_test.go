package main

import (
	"image/png"
	"os"
	"path/filepath"
	"testing"
)

// Dumps a couple of converted frames so the crop and axis can be eyeballed.
// Enable with CHARGEN_DUMP=<dir>.
func TestDumpSampleFrames(t *testing.T) {
	dir := os.Getenv("CHARGEN_DUMP")
	sheetPath := os.Getenv("CHARGEN_SHEET")
	if dir == "" || sheetPath == "" {
		t.Skip("set CHARGEN_DUMP and CHARGEN_SHEET to dump sample frames")
	}
	src := loadSource(t)
	sheet, err := LoadPNG(sheetPath)
	if err != nil {
		t.Fatalf("sheet: %v", err)
	}
	if err := os.MkdirAll(dir, 0o755); err != nil {
		t.Fatalf("mkdir: %v", err)
	}

	for _, name := range []string{"idle", "punch", "strongkick"} {
		a := src.Anim(name)
		if a == nil {
			continue
		}
		for i, f := range a.Frames {
			if i > 1 {
				break
			}
			img := CropSheet(sheet, f.Src)
			out, err := os.Create(filepath.Join(dir, name+"_"+string(rune('0'+i))+".png"))
			if err != nil {
				t.Fatalf("create: %v", err)
			}
			if err := png.Encode(out, img); err != nil {
				t.Fatalf("encode: %v", err)
			}
			out.Close()
		}
	}
}
