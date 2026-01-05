using System.Collections.Generic;

public class TilingArea
{
	public readonly List<TilingSource> List = new List<TilingSource>();

	private int x;

	private int y = -1;

	private int mx = 7;

	private int my = 7;

	private int ly;

	private int lx;

	public int Width => mx / 7 * 4096;

	public int Height => my / 7 * 4096;

	public void Add(TilingSource source)
	{
		y++;
		if (y == my)
		{
			if (x + 1 == mx)
			{
				if (mx == my)
				{
					x++;
					y = 0;
					mx *= 2;
					ly = 0;
				}
				else
				{
					x = lx;
					ly = my;
					my *= 2;
				}
			}
			else
			{
				y = ly;
				x++;
			}
		}
		source.Dst.x = x;
		source.Dst.y = y;
		List.Add(source);
	}

	public void Clear()
	{
		List.Clear();
		x = 0;
		y = -1;
		mx = 7;
		my = 7;
		ly = 0;
		lx = 0;
	}
}
