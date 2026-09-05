package main

import (
	"encoding/binary"
	"fmt"
	"sort"
	"strings"
)

// Helpers for picking apart an undocumented binary container.

func u16(b []byte, off int) uint16 {
	if off+2 > len(b) {
		return 0
	}
	return binary.LittleEndian.Uint16(b[off:])
}

func u32(b []byte, off int) uint32 {
	if off+4 > len(b) {
		return 0
	}
	return binary.LittleEndian.Uint32(b[off:])
}

func hexDump(b []byte, start, n int) string {
	var sb strings.Builder
	for i := start; i < start+n && i < len(b); i += 16 {
		fmt.Fprintf(&sb, "%08X  ", i)
		for j := 0; j < 16; j++ {
			if i+j < len(b) {
				fmt.Fprintf(&sb, "%02X ", b[i+j])
			} else {
				sb.WriteString("   ")
			}
			if j == 7 {
				sb.WriteByte(' ')
			}
		}
		sb.WriteString(" |")
		for j := 0; j < 16 && i+j < len(b); j++ {
			c := b[i+j]
			if c >= 32 && c < 127 {
				sb.WriteByte(c)
			} else {
				sb.WriteByte('.')
			}
		}
		sb.WriteString("|\n")
	}
	return sb.String()
}

// findRecordStride looks for a repeating layout by testing how often a value
// recurs at a fixed spacing. Container formats almost always begin with a table
// of fixed-size records, and its stride is the first thing worth knowing.
func findRecordStride(b []byte, from, limit int) []int {
	type score struct {
		stride, hits int
	}
	var scores []score

	for stride := 4; stride <= 64; stride += 2 {
		hits := 0
		for off := from; off+stride*2 < from+limit && off+stride*2 < len(b); off += stride {
			if u32(b, off) == u32(b, off+stride) {
				hits++
			}
		}
		scores = append(scores, score{stride, hits})
	}
	sort.Slice(scores, func(i, j int) bool { return scores[i].hits > scores[j].hits })

	out := make([]int, 0, 5)
	for i := 0; i < 5 && i < len(scores); i++ {
		out = append(out, scores[i].stride)
	}
	return out
}

// plausibleOffsets reports how many uint32 values in a window look like file
// offsets, which is how an index table gives itself away.
func plausibleOffsets(b []byte, from, count, stride int) (valid, ascending int) {
	prev := uint32(0)
	for i := 0; i < count; i++ {
		off := from + i*stride
		v := u32(b, off)
		if v > 0 && int(v) < len(b) {
			valid++
			if v > prev {
				ascending++
			}
			prev = v
		}
	}
	return
}
