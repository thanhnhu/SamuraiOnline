package main

import (
	"bytes"
	"encoding/binary"
	"image"
	"image/png"
	"io"
	"os"
)

// Minimal SFF v2 writer, the same one chargen uses. Kept as a copy rather than
// a shared module because the two tools are separate Go modules and the format
// is finished; if either ever needs to change, change both.
//
// Ikemen accepts sprite payloads that are just embedded PNG files (format 12 =
// 32-bit RGBA), which skips the PCX/RLE/LZ5 encoders a classic SFF would need.
const (
	sffSignature   = "ElecbyteSpr\x00"
	sffHeaderSize  = 64
	sffSpriteHdr   = 28
	sffFormatPNG32 = 12
)

type SffSprite struct {
	Group  uint16
	Number uint16
	AxisX  int16
	AxisY  int16
	Img    *image.RGBA
}

func WriteSff(path string, sprites []SffSprite) error {
	f, err := os.Create(path)
	if err != nil {
		return err
	}
	defer f.Close()
	return writeSffTo(f, sprites)
}

func writeSffTo(w io.Writer, sprites []SffSprite) error {
	// Encode payloads first so the header can carry exact offsets. Compressed
	// formats carry a 4-byte length prefix that the reader skips before the
	// real payload, so PNG data has to sit behind one too.
	payloads := make([][]byte, len(sprites))
	for i, s := range sprites {
		var buf bytes.Buffer
		if err := png.Encode(&buf, s.Img); err != nil {
			return err
		}
		var framed bytes.Buffer
		putU32(&framed, uint32(buf.Len()))
		framed.Write(buf.Bytes())
		payloads[i] = framed.Bytes()
	}

	spriteHdrOfs := uint32(sffHeaderSize)
	dataOfs := spriteHdrOfs + uint32(len(sprites)*sffSpriteHdr)

	var out bytes.Buffer
	out.WriteString(sffSignature)
	out.Write([]byte{0, 0, 0, 2}) // version 2.0.0.0, low byte first
	putU32(&out, 0)
	for i := 0; i < 4; i++ {
		putU32(&out, 0)
	}
	putU32(&out, spriteHdrOfs)
	putU32(&out, uint32(len(sprites)))
	putU32(&out, 0) // no palette bank
	putU32(&out, 0)
	putU32(&out, dataOfs) // ldata block start
	putU32(&out, 0)       // ldata length, unused by the reader
	putU32(&out, 0)       // tdata block start

	if out.Len() != sffHeaderSize {
		panic("sff header size drifted")
	}

	var rel uint32
	for i, s := range sprites {
		b := s.Img.Bounds()
		putU16(&out, s.Group)
		putU16(&out, s.Number)
		putU16(&out, uint16(b.Dx()))
		putU16(&out, uint16(b.Dy()))
		putI16(&out, s.AxisX)
		putI16(&out, s.AxisY)
		putU16(&out, uint16(i)) // link to self: not a linked sprite
		out.WriteByte(sffFormatPNG32)
		out.WriteByte(32)
		putU32(&out, rel) // relative to the ldata block
		putU32(&out, uint32(len(payloads[i])))
		putU16(&out, 0) // palette index
		putU16(&out, 0) // flags bit0 = 0 -> offsets are ldata-relative
		rel += uint32(len(payloads[i]))
	}
	for _, p := range payloads {
		out.Write(p)
	}

	_, err := w.Write(out.Bytes())
	return err
}

func putU16(b *bytes.Buffer, v uint16) { _ = binary.Write(b, binary.LittleEndian, v) }
func putI16(b *bytes.Buffer, v int16)  { _ = binary.Write(b, binary.LittleEndian, v) }
func putU32(b *bytes.Buffer, v uint32) { _ = binary.Write(b, binary.LittleEndian, v) }
