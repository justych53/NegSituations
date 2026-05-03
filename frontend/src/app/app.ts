import { Component, OnInit, signal } from '@angular/core';
import { DatabaseService } from './services/database';
import { RouterLink, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  templateUrl: './app.html',
  styleUrls: ['./app.css'],
  imports: [RouterOutlet, RouterLink]
})
export class AppComponent {
}
export class App {
  protected readonly title = signal('neg-situations');
}
