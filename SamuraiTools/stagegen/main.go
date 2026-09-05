// stagegen turns Samurai Shodown II stage data into Ikemen stages.
//
// It reads the .BGR backgrounds and .WAV music from a copy of the game you
// supply. Nothing it reads or writes belongs in this repository: the game is
// still sold by SNK, so the conversion happens on your machine, the same way
// chargen handles characters.
//
//	stagegen -data "$GAME/DATA" -out ../../Ikemen-GO/stages/samsho2
//
// Then play one:
//
//	./Ikemen_GO_Linux -p1 jubei -p2 jubei2 -s samsho2/B001
package main

import (
	"flag"
	"fmt"
	"image/png"
	"os"
	"path/filepath"
	"sort"
	"strings"
)

func main() {
	data := flag.String("data", "", "the game's DATA directory")
	out := flag.String("out", "", "where to write the converted stages")
	snapshot := flag.Int("palette", 0, "which palette snapshot in GAME1.PRG to use, 0..5")
	delta := flag.Float64("delta", 1, "how far layers scroll per unit of camera movement")
	withMusic := flag.Bool("music", true, "copy the game's .WAV music next to the stages")
	dumpOnly := flag.Bool("dump", false, "only write the backgrounds as PNG, no stage files")
	flag.Parse()

	if *data == "" || *out == "" {
		flag.Usage()
		os.Exit(2)
	}
	if err := run(*data, *out, *snapshot, *delta, *withMusic, *dumpOnly); err != nil {
		fmt.Fprintf(os.Stderr, "stagegen: %v\n", err)
		os.Exit(1)
	}
}

func run(data, out string, snapshot int, delta float64, withMusic, dumpOnly bool) error {
	prg, err := os.ReadFile(filepath.Join(data, "GAME1.PRG"))
	if err != nil {
		return fmt.Errorf("the palette lives in GAME1.PRG: %w", err)
	}
	pal, err := LoadPalette(prg, snapshot)
	if err != nil {
		return err
	}

	files, err := filepath.Glob(filepath.Join(data, "*.BGR"))
	if err != nil {
		return err
	}
	if len(files) == 0 {
		return fmt.Errorf("no .BGR files in %s", data)
	}
	sort.Strings(files)

	if err := os.MkdirAll(out, 0o755); err != nil {
		return err
	}

	music := map[string]string{}
	if withMusic && !dumpOnly {
		music, err = copyMusic(data, out)
		if err != nil {
			return err
		}
	}

	var stages, decoded, failed int
	for _, path := range files {
		raw, err := os.ReadFile(path)
		if err != nil {
			return err
		}
		archive, err := Parse(raw)
		if err != nil {
			fmt.Printf("%-10s %v\n", filepath.Base(path), err)
			failed++
			continue
		}
		base := strings.TrimSuffix(filepath.Base(path), filepath.Ext(path))

		var layers []Layer
		for i, rec := range archive.Records {
			img, err := archive.Decode(rec, pal)
			if err != nil {
				fmt.Printf("%-10s layer %d: %v\n", base, i, err)
				failed++
				continue
			}
			layers = append(layers, Layer{ID: rec.ID, Image: img})
			decoded++

			if dumpOnly {
				name := fmt.Sprintf("%s_%d_id%d.png", base, i, rec.ID)
				fh, err := os.Create(filepath.Join(out, name))
				if err != nil {
					return err
				}
				err = png.Encode(fh, img)
				fh.Close()
				if err != nil {
					return err
				}
			}
		}
		if dumpOnly || len(layers) == 0 {
			continue
		}

		opt := StageOptions{Name: base, Delta: delta, Music: music[base]}
		if err := WriteStage(out, base, layers, opt); err != nil {
			return err
		}
		note := "no music"
		if opt.Music != "" {
			note = filepath.Base(opt.Music)
		}
		fmt.Printf("%-10s %d layer(s), %-12s -> %s.def\n", base, len(layers), note, base)
		stages++
	}

	if dumpOnly {
		fmt.Printf("\n%d layers decoded, %d failed\n", decoded, failed)
	} else {
		fmt.Printf("\n%d stages written from %d layers, %d failed\n", stages, decoded, failed)
	}
	if failed > 0 {
		return fmt.Errorf("%d layers failed to decode", failed)
	}
	return nil
}

// copyMusic puts the game's .WAV files beside the stages and returns, per stage
// base name, the path a .def should reference.
//
// Which track belongs to which stage is NOT decoded. The game loads music by an
// index that comes from a table this tool has not found, so the pairing here is
// by matching numbers: B001.BGR takes SND_01.WAV. That is a guess, and an easy
// one to correct by editing bgmusic in the generated .def.
func copyMusic(data, out string) (map[string]string, error) {
	wavs, err := filepath.Glob(filepath.Join(data, "SND_*.WAV"))
	if err != nil {
		return nil, err
	}
	if len(wavs) == 0 {
		return map[string]string{}, nil
	}

	soundDir := filepath.Join(out, "sound")
	if err := os.MkdirAll(soundDir, 0o755); err != nil {
		return nil, err
	}

	// The engine resolves bgmusic from its own root, so record the path from
	// there rather than from the output directory.
	prefix := relativeToEngine(out)

	byNumber := map[string]string{}
	for _, w := range wavs {
		name := filepath.Base(w)
		b, err := os.ReadFile(w)
		if err != nil {
			return nil, err
		}
		if err := os.WriteFile(filepath.Join(soundDir, name), b, 0o644); err != nil {
			return nil, err
		}
		num := strings.TrimSuffix(strings.TrimPrefix(name, "SND_"), ".WAV")
		byNumber[strings.ToUpper(num)] = prefix + "/sound/" + name
	}

	stageMusic := map[string]string{}
	bgrs, _ := filepath.Glob(filepath.Join(data, "*.BGR"))
	for _, g := range bgrs {
		base := strings.TrimSuffix(filepath.Base(g), filepath.Ext(g))
		if len(base) < 3 {
			continue
		}
		// B001 -> 01, B00A -> 0A, B100 -> 00
		if m, ok := byNumber[strings.ToUpper(base[len(base)-2:])]; ok {
			stageMusic[base] = m
		}
	}
	return stageMusic, nil
}

// relativeToEngine turns an output directory into the path the engine will see,
// which is whatever follows the Ikemen-GO root.
func relativeToEngine(out string) string {
	abs, err := filepath.Abs(out)
	if err != nil {
		return filepath.Base(out)
	}
	parts := strings.Split(filepath.ToSlash(abs), "/")
	for i := len(parts) - 1; i >= 0; i-- {
		if parts[i] == "Ikemen-GO" {
			return strings.Join(parts[i+1:], "/")
		}
	}
	return filepath.Base(out)
}
