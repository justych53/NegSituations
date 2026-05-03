import { Injectable } from '@angular/core';
import initSqlJs, { Database } from 'sql.js';
import { get, set } from 'idb-keyval';

@Injectable({ providedIn: 'root' })
export class DatabaseService {
  private db!: Database;
  private initialized = false;
  private initializing = false;

  async init(): Promise<void> {
    if (this.initialized) return;
    
    // Если уже инициализируется, ждём
    if (this.initializing) {
      while (!this.initialized) {
        await new Promise(resolve => setTimeout(resolve, 50));
      }
      return;
    }

    this.initializing = true;

    try {
      const SQL = await initSqlJs({
        locateFile: () => '/assets/sql-wasm.wasm'
    });

      // Пытаемся загрузить сохранённую базу из IndexedDB
      const saved = await get('neg-situations.db');
      if (saved) {
        this.db = new SQL.Database(saved);
      } else {
        this.db = new SQL.Database();
      }

      // Создаём таблицы по твоей схеме
      this.db.run(`
        CREATE TABLE IF NOT EXISTS FailureRecords (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          DescFailure TEXT NOT NULL,
          ResInvest TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS Participants (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          Name TEXT NOT NULL,
          Position TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS FailureParticipants (
          FailureRecordId INTEGER NOT NULL,
          ParticipantId INTEGER NOT NULL,
          PRIMARY KEY (FailureRecordId, ParticipantId),
          FOREIGN KEY (FailureRecordId) REFERENCES FailureRecords(Id) ON DELETE CASCADE,
          FOREIGN KEY (ParticipantId) REFERENCES Participants(Id) ON DELETE CASCADE
        );
      `);

      this.initialized = true;
    } finally {
      this.initializing = false;
    }
  }

  private async saveToStore(): Promise<void> {
    const data = this.db.export();
    await set('neg-situations.db', data.buffer);
  }

  // ====== CRUD FailureRecords ======
  async addFailureRecord(desc: string, res: string, participantIds: number[]): Promise<number> {
    if (!this.initialized) await this.init();

    this.db.run('INSERT INTO FailureRecords (DescFailure, ResInvest) VALUES (?, ?)', [desc, res]);
    const result = this.db.exec('SELECT last_insert_rowid()');
    const recordId = result[0].values[0][0] as number;

    for (const pid of participantIds) {
      this.db.run(
        'INSERT OR IGNORE INTO FailureParticipants (FailureRecordId, ParticipantId) VALUES (?, ?)',
        [recordId, pid]
      );
    }

    await this.saveToStore();
    return recordId;
  }

  async getAllFailureRecords(): Promise<any[]> {
    if (!this.initialized) await this.init();

    const stmt = this.db.prepare('SELECT * FROM FailureRecords');
    const records = [];
    while (stmt.step()) records.push(stmt.getAsObject());
    stmt.free();
    return records;
  }

  async getFailureRecordById(id: number): Promise<any> {
    if (!this.initialized) await this.init();

    const stmt = this.db.prepare('SELECT * FROM FailureRecords WHERE Id = ?');
    stmt.bind([id]);
    let record = null;
    if (stmt.step()) record = stmt.getAsObject();
    stmt.free();
    return record;
  }

  async getParticipantsForRecord(recordId: number): Promise<any[]> {
    if (!this.initialized) await this.init();

    const stmt = this.db.prepare(`
      SELECT p.* FROM Participants p
      JOIN FailureParticipants fp ON p.Id = fp.ParticipantId
      WHERE fp.FailureRecordId = ?
    `);
    stmt.bind([recordId]);
    const participants = [];
    while (stmt.step()) participants.push(stmt.getAsObject());
    stmt.free();
    return participants;
  }

  async updateFailureRecord(id: number, desc: string, res: string, participantIds: number[]): Promise<void> {
    if (!this.initialized) await this.init();

    this.db.run('UPDATE FailureRecords SET DescFailure = ?, ResInvest = ? WHERE Id = ?', [desc, res, id]);
    
    // Удаляем старые связи
    this.db.run('DELETE FROM FailureParticipants WHERE FailureRecordId = ?', [id]);
    
    // Добавляем новые
    for (const pid of participantIds) {
      this.db.run(
        'INSERT OR IGNORE INTO FailureParticipants (FailureRecordId, ParticipantId) VALUES (?, ?)',
        [id, pid]
      );
    }

    await this.saveToStore();
  }

  async deleteFailureRecord(id: number): Promise<void> {
    if (!this.initialized) await this.init();

    this.db.run('DELETE FROM FailureRecords WHERE Id = ?', [id]);
    await this.saveToStore();
  }

  // ====== CRUD Participants ======
  async addParticipant(name: string, position: string): Promise<number> {
    if (!this.initialized) await this.init();

    this.db.run('INSERT INTO Participants (Name, Position) VALUES (?, ?)', [name, position]);
    const result = this.db.exec('SELECT last_insert_rowid()');
    const id = result[0].values[0][0] as number;
    await this.saveToStore();
    return id;
  }

  async getAllParticipants(): Promise<any[]> {
    if (!this.initialized) await this.init();

    const stmt = this.db.prepare('SELECT * FROM Participants');
    const participants = [];
    while (stmt.step()) participants.push(stmt.getAsObject());
    stmt.free();
    return participants;
  }

  async getParticipantById(id: number): Promise<any> {
    if (!this.initialized) await this.init();

    const stmt = this.db.prepare('SELECT * FROM Participants WHERE Id = ?');
    stmt.bind([id]);
    let participant = null;
    if (stmt.step()) participant = stmt.getAsObject();
    stmt.free();
    return participant;
  }

  async updateParticipant(id: number, name: string, position: string): Promise<void> {
    if (!this.initialized) await this.init();

    this.db.run('UPDATE Participants SET Name = ?, Position = ? WHERE Id = ?', [name, position, id]);
    await this.saveToStore();
  }

  async deleteParticipant(id: number): Promise<void> {
    if (!this.initialized) await this.init();

    this.db.run('DELETE FROM Participants WHERE Id = ?', [id]);
    await this.saveToStore();
  }
}
