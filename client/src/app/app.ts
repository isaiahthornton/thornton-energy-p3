import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Navbar } from './components/navbar/navbar';
import { Footer } from './components/footer/footer';
import { ChatWidget } from './components/chat-widget/chat-widget';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Navbar, Footer, ChatWidget],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {}
