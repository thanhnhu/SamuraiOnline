package polling

import (
	"time"

	"github.com/ikemen-engine/ggpo/internal/buffer"
)

type FuncTimeType func() int64

func DefaultTime() int64 {
	return time.Now().UnixMilli()
}

type Poll struct {
	startTime   int64
	handleCount int
	loopSinks   buffer.StaticBuffer[PollSinkCb]
}

type Poller interface {
	RegisterLoop(sink PollSink, cookie []byte)
	Pump(timeFunc ...FuncTimeType) bool
}

type PollSinkCb struct {
	sink   PollSink
	cookie []byte
}

type PollSink interface {
	OnLoopPoll(timeFunc FuncTimeType) bool
}

// MaxLoopSinks has to cover every endpoint that registers a loop: one per
// player plus one per spectator. It mirrors ggpo.MaxPlayers + ggpo.MaxSpectators,
// which cannot be imported here without a cycle. Sizing it any smaller makes
// AddSpectator panic well before MaxSpectators is reached.
const MaxLoopSinks = 4 + 32 + 1

func NewPoll() Poll {
	return Poll{
		startTime:   0,
		handleCount: 0,
		loopSinks:   buffer.NewStaticBuffer[PollSinkCb](MaxLoopSinks),
	}
}

func (p *Poll) RegisterLoop(sink PollSink, cookie []byte) {
	err := p.loopSinks.PushBack(
		PollSinkCb{
			sink:   sink,
			cookie: cookie})
	if err != nil {
		panic(err)
	}
}

func (p *Poll) Pump(timeFunc ...FuncTimeType) bool {
	finished := false
	if p.startTime == 0 {
		p.startTime = time.Now().UnixMilli()
	}
	for i := 0; i < p.loopSinks.Size(); i++ {
		cb, err := p.loopSinks.Get(i)
		if err != nil {
			panic(err)
		}
		if len(timeFunc) != 0 {
			finished = !(cb.sink.OnLoopPoll(timeFunc[0]) || finished)
		} else {
			finished = !(cb.sink.OnLoopPoll(DefaultTime) || finished)
		}
	}
	return finished

}
