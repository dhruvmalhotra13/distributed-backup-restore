"use client";

import { useEffect, useRef, useState } from "react";
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import { API_BASE } from "./api";
import type { ProgressUpdate } from "./types";

export type ProgressMap = Record<string, ProgressUpdate>;

/**
 * Subscribes to the backend SignalR hub and exposes the latest progress
 * snapshot per job id, keyed for O(1) lookup.
 */
export function useProgress() {
  const [progress, setProgress] = useState<ProgressMap>({});
  const [connected, setConnected] = useState(false);
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE}/hubs/progress`)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    connection.on("JobProgress", (update: ProgressUpdate) => {
      setProgress((prev) => ({ ...prev, [update.jobId]: update }));
    });

    connection.onreconnected(() => setConnected(true));
    connection.onclose(() => setConnected(false));

    connection
      .start()
      .then(() => setConnected(true))
      .catch(() => setConnected(false));

    return () => {
      if (connection.state !== HubConnectionState.Disconnected) {
        connection.stop();
      }
    };
  }, []);

  return { progress, connected };
}
