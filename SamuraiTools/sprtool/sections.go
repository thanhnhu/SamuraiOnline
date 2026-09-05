package main

import (
	"fmt"
	"math"
	"strings"
)

// Section boundaries in an unknown container show up as a change in byte
// statistics: index tables are sparse and low entropy, compressed or packed
// pixels are dense and high entropy.

type block struct {
	off      int
	entropy  float64
	zeroFrac float64
}

func scanBlocks(b []byte, size int) []block {
	var out []block
	for off := 0; off < len(b); off += size {
		end := off + size
		if end > len(b) {
			end = len(b)
		}
		out = append(out, block{
			off:      off,
			entropy:  entropy(b[off:end]),
			zeroFrac: zeroFraction(b[off:end]),
		})
	}
	return out
}

func entropy(b []byte) float64 {
	var counts [256]int
	for _, c := range b {
		counts[c]++
	}
	e := 0.0
	n := float64(len(b))
	for _, c := range counts {
		if c == 0 {
			continue
		}
		p := float64(c) / n
		e -= p * math.Log2(p)
	}
	return e
}

func zeroFraction(b []byte) float64 {
	z := 0
	for _, c := range b {
		if c == 0 {
			z++
		}
	}
	return float64(z) / float64(len(b))
}

// summariseSections collapses adjacent blocks with similar statistics so the
// layout is readable at a glance.
func summariseSections(blocks []block, blockSize int) string {
	var sb strings.Builder
	if len(blocks) == 0 {
		return ""
	}
	kind := func(bl block) string {
		switch {
		case bl.zeroFrac > 0.6:
			return "sparse/table"
		case bl.entropy > 7.0:
			return "dense/packed"
		default:
			return "mixed"
		}
	}

	start := blocks[0].off
	cur := kind(blocks[0])
	for i := 1; i <= len(blocks); i++ {
		if i < len(blocks) && kind(blocks[i]) == cur {
			continue
		}
		end := start + blockSize
		if i < len(blocks) {
			end = blocks[i].off
		} else {
			end = blocks[len(blocks)-1].off + blockSize
		}
		fmt.Fprintf(&sb, "  %08X - %08X  %-13s (%d bytes)\n", start, end, cur, end-start)
		if i < len(blocks) {
			start = blocks[i].off
			cur = kind(blocks[i])
		}
	}
	return sb.String()
}
