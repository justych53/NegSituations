import { Component } from '@angular/core';

@Component({
  selector: 'saaty-scale',
  standalone: true,
  template: `
    <details style="margin-bottom: 10px;">
      <summary><strong>Шкала Саати</strong></summary>
      <table border="1" cellpadding="5" style="margin-top: 5px;">
        <tr><th>1</th><td>Равная важность</td></tr>
        <tr><th>3</th><td>Умеренное превосходство</td></tr>
        <tr><th>5</th><td>Существенное превосходство</td></tr>
        <tr><th>7</th><td>Значительное превосходство</td></tr>
        <tr><th>9</th><td>Абсолютное превосходство</td></tr>
        <tr><th>2,4,6,8</th><td>Промежуточные</td></tr>
        <tr><th>1/x</th><td>Обратные — менее важно</td></tr>
      </table>
    </details>
  `
})
export class SaatyScaleComponent {}