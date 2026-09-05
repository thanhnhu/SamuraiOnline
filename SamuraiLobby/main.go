package main

import (
	"context"
	"errors"
	"flag"
	"log"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"
)

func main() {
	addr := flag.String("addr", ":8080", "HTTP address to listen on")
	relayAddr := flag.String("relay-addr", ":8081", "UDP address for the relay and address-discovery service")
	relayTCPAddr := flag.String("relay-tcp-addr", ":8081", "TCP address for relaying match setup streams")
	relayHost := flag.String("relay-host", "127.0.0.1", "relay host advertised to clients")
	trustProxy := flag.Bool("trust-proxy", false,
		"read the client address from X-Forwarded-For; only enable behind a proxy that overwrites this header")
	flag.Parse()

	store := NewStore()
	relay := NewRelay()
	relay.OnAddrRequest = store.RecordUDPAddr
	boundRelay, err := relay.Listen(*relayAddr)
	if err != nil {
		log.Fatalf("relay listen: %v", err)
	}
	logRelayListening(boundRelay)
	go relay.Serve()
	defer relay.Close()

	tcpRelay := NewTCPRelay(relay.Known)
	boundTCP, err := tcpRelay.Listen(*relayTCPAddr)
	if err != nil {
		log.Fatalf("tcp relay listen: %v", err)
	}
	log.Printf("tcp relay listening on %s", boundTCP)
	go tcpRelay.Serve()
	defer tcpRelay.Close()

	srv := &http.Server{
		Addr:              *addr,
		Handler:           NewAPI(store, relay, *relayHost, boundRelay.Port, boundTCP.Port, *trustProxy).Routes(),
		ReadHeaderTimeout: 5 * time.Second,
		ReadTimeout:       10 * time.Second,
		WriteTimeout:      10 * time.Second,
		IdleTimeout:       60 * time.Second,
	}

	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()

	go func() {
		ticker := time.NewTicker(5 * time.Second)
		defer ticker.Stop()
		for {
			select {
			case <-ctx.Done():
				return
			case <-ticker.C:
				store.Sweep()
				relay.Sweep()
			}
		}
	}()

	go func() {
		log.Printf("lobby server listening on %s (trustProxy=%v)", *addr, *trustProxy)
		if err := srv.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
			log.Fatalf("listen: %v", err)
		}
	}()

	<-ctx.Done()
	log.Println("shutting down")

	shutdownCtx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	if err := srv.Shutdown(shutdownCtx); err != nil {
		log.Printf("shutdown: %v", err)
	}
}
