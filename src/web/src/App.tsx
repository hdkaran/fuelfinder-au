import { BrowserRouter, Routes, Route } from 'react-router-dom';

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<div>Home — Phase 4</div>} />
        <Route path="/stations/:id" element={<div>Station detail — Phase 4</div>} />
        <Route path="/report/:stationId" element={<div>Report — Phase 5</div>} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
