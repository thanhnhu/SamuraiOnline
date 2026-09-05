package main

import (
	"fmt"
	"sort"
	"strings"
)

// The blitter computes framebuffer = pixelByte + bank, masked to 12 bits, and
// that 12-bit value indexes a 4096-entry colour table. If the game keeps the
// Neo Geo convention of 16 colours per palette then every bank is a multiple of
// 16 and no pixel value ever exceeds 15. Both facts are checkable here without
// having found the palette itself, and together they pin down how wide a
// palette each sprite actually needs.

type pixelStats struct {
	hist       [256]int
	maxIndex   int
	sprites    int
	failed     int
	banks      map[uint16]int
	bankMax    map[uint16]int
	misaligned map[uint16]int
}

func collectStats(b []byte, limit int) *pixelStats {
	hdr, descs := mustDescribe(b)
	st := &pixelStats{
		banks:      map[uint16]int{},
		bankMax:    map[uint16]int{},
		misaligned: map[uint16]int{},
	}
	for _, d := range descs {
		if limit > 0 && st.sprites >= limit {
			break
		}
		s, err := Extract(b, hdr, d)
		if err != nil {
			st.failed++
			continue
		}
		st.sprites++
		st.banks[d.Bank]++
		if d.Bank%16 != 0 {
			st.misaligned[d.Bank]++
		}
		for i, op := range s.Opaque {
			if !op {
				continue
			}
			v := int(s.Pix[i])
			st.hist[v]++
			if v > st.maxIndex {
				st.maxIndex = v
			}
			if v > st.bankMax[d.Bank] {
				st.bankMax[d.Bank] = v
			}
		}
	}
	return st
}

func (st *pixelStats) String() string {
	var sb strings.Builder
	fmt.Fprintf(&sb, "%d sprites decoded, %d failed\n", st.sprites, st.failed)
	fmt.Fprintf(&sb, "highest palette index seen: %d\n", st.maxIndex)
	fmt.Fprintf(&sb, "banks not a multiple of 16: %d\n", len(st.misaligned))

	used := 0
	for _, c := range st.hist {
		if c > 0 {
			used++
		}
	}
	fmt.Fprintf(&sb, "distinct indices used: %d\n\n", used)

	fmt.Fprintf(&sb, "index histogram (first 32):\n")
	total := 0
	for _, c := range st.hist {
		total += c
	}
	for i := 0; i < 32; i++ {
		if st.hist[i] == 0 {
			continue
		}
		fmt.Fprintf(&sb, "  %3d  %9d  %5.2f%%\n", i, st.hist[i],
			100*float64(st.hist[i])/float64(total))
	}

	fmt.Fprintf(&sb, "\nper-bank widest index:\n")
	keys := make([]uint16, 0, len(st.bankMax))
	for k := range st.bankMax {
		keys = append(keys, k)
	}
	sort.Slice(keys, func(i, j int) bool { return keys[i] < keys[j] })
	for _, k := range keys {
		fmt.Fprintf(&sb, "  bank %5d (0x%04X, /16=%s)  max index %3d  %d sprites\n",
			k, k, alignNote(k), st.bankMax[k], st.banks[k])
	}
	return sb.String()
}

func alignNote(bank uint16) string {
	if bank%16 == 0 {
		return fmt.Sprintf("%d", bank/16)
	}
	return "NO"
}
