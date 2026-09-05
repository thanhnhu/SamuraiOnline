package main

import (
	"fmt"
	"strings"
)

// Row-by-row trace using the same reading as the game's blitter. Each row is
// bounded by its own length prefix, so a mistake shows up as a row whose runs
// do not add up to the sprite width rather than as runaway parsing.

type rowTrace struct {
	row      int
	startOff int
	length   int
	x        int
	runs     []string
}

func traceRows(b []byte, off, width, height, maxRows int) []rowTrace {
	var out []rowTrace
	rowStart := off

	for y := 0; y < height && y < maxRows; y++ {
		if rowStart+rowLengthPrefix > len(b) {
			return out
		}
		rowLen := int(u16(b, rowStart))
		if rowLen < rowLengthPrefix || rowStart+rowLen > len(b) {
			out = append(out, rowTrace{row: y, startOff: rowStart, length: rowLen,
				runs: []string{"BAD LENGTH"}})
			return out
		}

		t := rowTrace{row: y, startOff: rowStart, length: rowLen}
		src := b[rowStart+rowLengthPrefix : rowStart+rowLen]
		x, p := 0, 0
		for p < len(src) {
			c := src[p]
			p++
			n := int(c & countMask)
			if c&literalFlag == 0 {
				t.runs = append(t.runs, fmt.Sprintf("skip%d", n))
			} else {
				t.runs = append(t.runs, fmt.Sprintf("lit%d", n))
				p += n
			}
			x += n
		}
		t.x = x
		out = append(out, t)
		rowStart += rowLen
	}
	return out
}

func reportTrace(b []byte, off, width, height, maxRows int) string {
	var sb strings.Builder
	fmt.Fprintf(&sb, "tracing %dx%d from 0x%X\n", width, height, off)
	exact, bad := 0, 0
	for _, t := range traceRows(b, off, width, height, maxRows) {
		mark := " "
		switch {
		case t.x == width:
			mark = "="
			exact++
		case t.x > width:
			mark = "!"
			bad++
		}
		fmt.Fprintf(&sb, " %s row %3d  0x%05X len=%-4d x=%-4d  %s\n",
			mark, t.row, t.startOff, t.length, t.x, strings.Join(t.runs, " "))
	}
	fmt.Fprintf(&sb, "  %d rows summed exactly to %d, %d overflowed\n", exact, width, bad)
	return sb.String()
}
