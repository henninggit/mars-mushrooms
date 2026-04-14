import { useEffect, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import type { BoardStateDto, RoundInfo, RoundScores } from '../types/game';

interface GameHubEvents {
  onBoardUpdated?: (playerId: string, state: BoardStateDto) => void;
  onAllBoardsSnapshot?: (boards: BoardStateDto[]) => void;
  onRoundStarted?: (round: RoundInfo) => void;
  onRoundEnded?: (scores: RoundScores) => void;
}

export function useGameHub(handlers: GameHubEvents) {
  const [connected, setConnected] = useState(false);
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/game')
      .withAutomaticReconnect()
      .build();

    connection.on('BoardUpdated', (playerId: string, state: BoardStateDto) => {
      handlers.onBoardUpdated?.(playerId, state);
    });

    connection.on('AllBoardsSnapshot', (boards: BoardStateDto[]) => {
      handlers.onAllBoardsSnapshot?.(boards);
    });

    connection.on('RoundStarted', (round: RoundInfo) => {
      handlers.onRoundStarted?.(round);
    });

    connection.on('RoundEnded', (scores: RoundScores) => {
      handlers.onRoundEnded?.(scores);
    });

    connection.onreconnected(() => setConnected(true));
    connection.onclose(() => setConnected(false));

    connection.start()
      .then(() => setConnected(true))
      .catch(err => console.error('SignalR connection failed:', err));

    connectionRef.current = connection;

    return () => {
      connection.stop();
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return { connected };
}
